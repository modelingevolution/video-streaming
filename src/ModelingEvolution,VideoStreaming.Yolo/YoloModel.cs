using Microsoft.ML.OnnxRuntime;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelingEvolution.VideoStreaming;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Microsoft.Extensions.Options;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;
using Emgu.CV.Util;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution_VideoStreaming.Yolo
{
    internal readonly struct TensorShape(int[] shape)
    {
        public int Length { get; } = GetSizeForShape(shape);

        public int[] Dimensions { get; } = shape;

        public long[] Dimensions64 { get; } = [.. shape.Select(x => (long)x)];

        private static int GetSizeForShape(ReadOnlySpan<int> shape)
        {
            var product = 1;

            for (var i = 0; i < shape.Length; i++)
            {
                var dimension = shape[i];

                if (dimension < 0)
                {
                    throw new ArgumentOutOfRangeException($"Shape must not have negative elements: {dimension}");
                }

                product = checked(product * dimension);
            }

            return product;
        }
    }

    internal class SessionTensorInfo
    {
        public TensorShape Input0 { get; }

        public TensorShape Output0 { get; }

        public TensorShape? Output1 { get; }

        public SessionTensorInfo(InferenceSession session)
        {
            var inputMetadata = session.InputMetadata.Values;
            var outputMetadata = session.OutputMetadata.Values;

            Input0 = new TensorShape(inputMetadata.First().Dimensions);
            Output0 = new TensorShape(outputMetadata.First().Dimensions);

            if (session.OutputMetadata.Count == 2)
            {
                Output1 = new TensorShape(outputMetadata.Last().Dimensions);
            }
        }
    }


    internal class DenseTensorOwner<T>(IMemoryOwner<T> owner, ReadOnlySpan<int> dimensions) : IDisposable
    {
        private DenseTensor<T>? _tensor = new(owner.Memory, dimensions);

        public DenseTensor<T> Tensor
        {
            get
            {
                ObjectDisposedException.ThrowIf(_tensor is null, this);
                return _tensor;
            }
        }

        public void Dispose()
        {
            if (_tensor == null) 
                return;
            _tensor = null;
            owner.Dispose();
        }
    }

    internal static class MemPoolExtensions
    {
        class MemoryOwner<T>(IMemoryOwner<T> inner, int size) : IMemoryOwner<T>
        {
            public void Dispose() => inner.Dispose();

            public Memory<T> Memory => inner.Memory.Slice(0, size);
        }
        public static IMemoryOwner<T> Exact<T>(this IMemoryOwner<T> m, int size)
        {
            return new MemoryOwner<T>(m, size);
        }
        public static DenseTensorOwner<T> AllocateTensor<T>(this MemoryPool<T> allocator,
            in TensorShape shape, bool clean = false)
        {
            var mOwn = allocator.Rent(shape.Length).Exact(shape.Length);
            if (clean) mOwn.Memory.Span.Fill(default(T));
            return new DenseTensorOwner<T>(mOwn, shape.Dimensions);
        }
    }

    internal class YoloRawOutput(DenseTensorOwner<float> output0, DenseTensorOwner<float>? output1) : IDisposable
    {
        private bool _disposed;

        public DenseTensor<float> Output0
        {
            get
            {
                EnsureNotDisposed();
                return output0.Tensor;
            }
        }

        public DenseTensor<float>? Output1
        {
            get
            {
                EnsureNotDisposed();
                return output1?.Tensor;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            output0.Dispose();
            output1?.Dispose();

            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    public interface IYoloPrediction<in TSelf> : IDisposable
    {
        internal static abstract string Describe(TSelf[] predictions);
    }

    public class YoloConfiguration
    {
        /// <summary>
        /// Specify the minimum confidence value for including a result. Default is 0.3f.
        /// </summary>
        public float Confidence { get; set; } = .3f;

        /// <summary>
        /// Specify the minimum IoU value for Non-Maximum Suppression (NMS). Default is 0.45f.
        /// </summary>
        public float IoU { get; set; } = .45f;


        public Size ImageSize { get; init; } = new Size(640, 640);

    }

    internal interface IParser<T>
    {
        public T[] ProcessTensorToResult(YoloRawOutput output, Rectangle rectangle, float threshold);
    }

    public class YoloPredictorOptions
    {
        public static YoloPredictorOptions Default { get; } = new();

#if GPURELEASE
    public bool UseCuda { get; init; } = true;
#else
        public bool UseCuda { get; init; }
#endif
        public int CudaDeviceId { get; init; }

        public SessionOptions? SessionOptions { get; init; }

        public YoloConfiguration? Configuration { get; init; }

        internal InferenceSession CreateSession(byte[] model)
        {
            if (UseCuda)
            {
                if (SessionOptions is not null)
                {
                    throw new InvalidOperationException("'UseCuda' and 'SessionOptions' cannot be used together");
                }

                return new InferenceSession(model, SessionOptions.MakeSessionOptionWithCudaProvider(CudaDeviceId));
            }

            if (SessionOptions != null)
            {
                return new InferenceSession(model, SessionOptions);
            }

            return new InferenceSession(model);
        }
    }

    public static class YoloModelFactory
    {
        public static IYoloModelRunner<Segmentation> LoadSegmentationModel(string segYoloModelFile)
        {
            var options = new YoloPredictorOptions();

            var model = File.ReadAllBytes(segYoloModelFile);
            var session = options.CreateSession(model);
            var metadata = new YoloMetadata(session);
            YoloConfiguration configuration = new YoloConfiguration();
            var bbParser = new RawBoundingBoxParser(metadata, configuration, new NonMaxSuppressionService());
            var segParser = new SegmentationParser(metadata, bbParser);
            return new YoloModelRunner<Segmentation>(segParser, session, configuration);
        }
    }


    public interface IYoloModelRunner<T> where T : IYoloPrediction<T>
    {
        ModelPerformance Performance { get; }
        unsafe YoloResult<T> Process(
            YuvFrame* frame, 
            Rectangle* interestArea, 
            float threshold);
    }

    internal class YoloModelRunner<T> : IYoloModelRunner<T> where T : IYoloPrediction<T>
    {
        private readonly IParser<T> _parser;
        private readonly YoloConfiguration _configuration;
        private readonly InferenceSession _session;
        private readonly SessionTensorInfo _tensorInfo;
        private readonly RunOptions _options = new();
        public ModelPerformance Performance { get; } = new();

        public YoloModelRunner(IParser<T> parser,
            InferenceSession session,
            YoloConfiguration? configuration = null)
        {
            _parser = parser;
            _configuration = configuration ?? new YoloConfiguration();

            _session = session;
            
            _tensorInfo = new SessionTensorInfo(_session);
        }

        public unsafe YoloResult<T> Process(
            YuvFrame* frame, 
            Rectangle* interestArea, float threshold)
        {
            using var binding = PreProcess(frame, interestArea, out var output);

            ProcessInterference(binding);

            return PostProcess(frame, interestArea, threshold, output);
        }

        private unsafe YoloResult<T> PostProcess(YuvFrame* frame, Rectangle* interestArea, float threshold, YoloRawOutput output)
        {
            // Now we have output we can process the output
            using var s = Performance.MeasurePostProcessing();
            
            var result = _parser.ProcessTensorToResult(output, *interestArea, threshold);
            output.Dispose();
            
            return new YoloResult<T>(result)
            {
                ImageSize = frame->Info.Size
            };
        }

        private void ProcessInterference(OrtIoBinding binding)
        {
            using var s = Performance.MeasureInterference();
            // Do the interference
            _session.RunWithBinding(_options, binding);
        }

        private unsafe OrtIoBinding PreProcess(YuvFrame* frame, Rectangle* interestArea, out YoloRawOutput output)
        {
            using var perf = Performance.MeasurePreProcessing();
            OrtIoBinding? binding = null;
            try
            {
                binding = _session.CreateIoBinding();
                output = CreateRawOutput(binding);
            
                using var input = MemoryPool<float>.Shared.AllocateTensor<float>(_tensorInfo.Input0, true);
                                    
                var s = _configuration.ImageSize;
                var target = input.Tensor;
                target.CopyInputFromYuvFrame(frame, interestArea, &s);

                // Create ort values
                var ortInput = CreateOrtValue(target.Buffer, _tensorInfo.Input0.Dimensions64);
            
                // Bind input to ort io binding
                binding.BindInput(_session.InputNames[0], ortInput);
                return binding;
            }
            catch
            {
                binding?.Dispose();
                throw;
            }
        }

        private YoloRawOutput CreateRawOutput(OrtIoBinding binding)
        {
            var output0Info = _tensorInfo.Output0;
            var output1Info = _tensorInfo.Output1;

            // Allocate output0 tensor buffer
            var output0 = MemoryPool<float>.Shared.AllocateTensor(output0Info);

            // Bind tensor buffer to ort binding
            binding.BindOutput(_session.OutputNames[0], CreateOrtValue(output0.Tensor.Buffer, output0Info.Dimensions64));

            if (output1Info != null)
            {
                // Allocate output1 tensor buffer
                var output1 = MemoryPool<float>.Shared.AllocateTensor(output1Info.Value);

                // Bind tensor buffer to ort binding
                binding.BindOutput(_session.OutputNames[1], CreateOrtValue(output1.Tensor.Buffer, output1Info.Value.Dimensions64));

                return new YoloRawOutput(output0, output1);
            }

            return new YoloRawOutput(output0, null);
        }

        private static OrtValue CreateOrtValue(Memory<float> buffer, long[] shape)
        {
            return OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, buffer, shape);
        }
    }
    public class YoloResult
    {
        public required Size ImageSize { get; init; }

    }
    public class YoloResult<TPrediction>(TPrediction[] predictions) : YoloResult, IDisposable, 
        IEnumerable<TPrediction> where TPrediction : IYoloPrediction<TPrediction>
    {
        public TPrediction this[int index] => predictions[index];

        public int Count => predictions.Length;

        public override string ToString() => TPrediction.Describe(predictions);
        public void Dispose()
        {
            for (int i = 0; i < predictions.Length; i++)
            {
                predictions[i]?.Dispose();
            }
        }

        #region Enumerator

        public IEnumerator<TPrediction> GetEnumerator()
        {
            foreach (var item in predictions)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
    public class YoloName(int id, string name)
    {
        public int Id { get; } = id;

        public string Name { get; } = name;

        public override string ToString()
        {
            return $"{Id}: '{Name}'";
        }
    }
    public abstract class YoloPrediction
    {
        public required YoloName Name { get; init; }

        public required float Confidence { get; init; }

        public override string ToString()
        {
            return $"{Name.Name} ({Confidence:N})";
        }
    }
    internal static class DetectionBoxesExtensions
    {
        public static string Summary(this IEnumerable<Detection> boxes)
        {
            var sort = boxes.Select(x => x.Name)
                .GroupBy(x => x.Id)
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Count()} {x.First().Name}");

            return string.Join(", ", sort);
        }
    }
    public class Detection : YoloPrediction, IYoloPrediction<Detection>
    {
        public required Rectangle Bounds { get; init; }

        static string IYoloPrediction<Detection>.Describe(Detection[] predictions) => predictions.Summary();

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public static class Ext
    {
        public static VectorU16[] ToVectorArray(this VectorOfPoint p)
        {
            var points = new VectorU16[p.Size];

            for (int i = 0; i < p.Size; i++)
            {
                points[i] = new VectorU16((ushort)p[i].X, (ushort)p[i].Y);
            }

            return points;
        }
        public static ManagedArray<VectorU16> ToArrayBuffer(this VectorOfPoint p)
        {
            var points = new ManagedArray<VectorU16>(p.Size);

            for (int i = 0; i < p.Size; i++) 
                points.Add(p[i]);

            return points;
        }
    }
    public class Segmentation : Detection, IYoloPrediction<Segmentation>
    {
        public required Mat Mask { get; init; }
        public required Rectangle InterestRegion { get; init; }
        public required float Threshold { get; init; }
        private SegmentationPolygon? _polygon;
        private bool _polygonComputed;
        
        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;
            Mask.Dispose();
            if(_polygonComputed)
                _polygon?.Dispose();
        }

        public SegmentationPolygon? Polygon
        {
            get
            {
                if (_polygonComputed) return _polygon;
                _polygon = ComputePolygon();
                _polygonComputed = true;
                if(_polygon != null)
                    Debug.Assert(_polygon.Polygon.Points.Count > 2);
                
                return _polygon;
            }
        }
        private SegmentationPolygon? ComputePolygon()
        {
            var sw = Stopwatch.StartNew(); sw.Start();
            
            var mat = Mask;
            using var thresholded = new Mat();
            CvInvoke.Threshold(mat, thresholded, Threshold * 255, 255, ThresholdType.Binary);
            
            using var contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(thresholded, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            
            if (contours.Size <= 0) 
                return null;
            
            var largestContour = contours[0];
            for (int i = 1; i < contours.Size; i++)
                if (CvInvoke.ContourArea(contours[i]) > CvInvoke.ContourArea(largestContour))
                    largestContour = contours[i];
                
            //using var hullIndices = new VectorOfPoint();
            //CvInvoke.ConvexHull(largestContour, hullIndices);
            //var points = hullIndices.ToVectorList();
            if (largestContour.Length == 0) return null;
            
            var points = largestContour.ToArrayBuffer();
            Debug.Assert(points.Count > 2);
            
            var result = new SegmentationPolygon(points,mat.Size);
            
            Debug.WriteLine("Compute Polygon in: " + sw.ElapsedMilliseconds);
            
            return result;
        }
        
        static string IYoloPrediction<Segmentation>.Describe(Segmentation[] predictions) => predictions.Summary();
    }

  
    
    internal interface IRawBoundingBoxParser
    {
        public T[] Parse<T>(DenseTensor<float> tensor) where T : IRawBoundingBox<T>;
    }
    internal interface IRawBoundingBox<TSelf> : IComparable<TSelf>
    {
        public int NameIndex { get; }

        public RectangleF Bounds { get; }

        public float Confidence { get; }

        public static abstract float CalculateIoU(ref TSelf box1, ref TSelf box2);

        public static abstract TSelf Parse(ref RawParsingContext context, int index, int nameIndex, float confidence);
    }
    public enum YoloArchitecture
    {
        YoloV8,
        YoloV10,
    }
    internal readonly ref struct RawParsingContext
    {
        public required YoloArchitecture Architecture { get; init; }

        public required DenseTensor<float> Tensor { get; init; }

        public required int Stride1 { get; init; }

        public int NameCount { get; init; }
    }
    public enum YoloTask
    {
        Obb,
        Detect,
        Segment,
        Pose,
        Classify
    }
    public class YoloMetadata
    {
        public string Author { get; }

        public string Description { get; }

        public string Version { get; }

        public int BatchSize { get; }

        public Size ImageSize { get; }

        public YoloTask Task { get; }

        public YoloName[] Names { get; }

        public YoloArchitecture Architecture { get; }

        internal YoloMetadata(InferenceSession session)
        {
            var metadata = session.ModelMetadata.CustomMetadataMap;

            Author = metadata["author"];
            Description = metadata["description"];
            Version = metadata["version"];

            Task = metadata["task"] switch
            {
                "obb" => YoloTask.Obb,
                "pose" => YoloTask.Pose,
                "detect" => YoloTask.Detect,
                "segment" => YoloTask.Segment,
                "classify" => YoloTask.Classify,
                _ => throw new InvalidOperationException("Unknow YoloV8 'task' value")
            };

            if (Task == YoloTask.Detect && session.OutputMetadata.Values.First().Dimensions[2] == 6) // YOLOv10 output shape => [<batch>, 300, 6]
            {
                Architecture = YoloArchitecture.YoloV10;
            }

            BatchSize = int.Parse(metadata["batch"]);
            ImageSize = ParseSize(metadata["imgsz"]);
            Names = ParseNames(metadata["names"]);
        }

        public static YoloMetadata Parse(InferenceSession session)
        {
            try
            {
                if (session.ModelMetadata.CustomMetadataMap["task"] == "pose")
                {
                    return new YoloPoseMetadata(session);
                }

                return new YoloMetadata(session);
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException("The metadata parsing failed, making sure you use an official YOLOv8 model", inner);
            }
        }

        #region Parsers

        private static Size ParseSize(string text)
        {
            text = text[1..^1]; // '[640, 640]' => '640, 640'

            var split = text.Split(", ");

            var y = int.Parse(split[0]);
            var x = int.Parse(split[1]);

            return new Size(x, y);
        }

        private static YoloName[] ParseNames(string text)
        {
            text = text[1..^1];

            var split = text.Split(", ");
            var count = split.Length;

            var names = new YoloName[count];

            for (int i = 0; i < count; i++)
            {
                var value = split[i];

                var valueSplit = value.Split(": ");

                var id = int.Parse(valueSplit[0]);
                var name = valueSplit[1][1..^1].Replace('_', ' ');

                names[i] = new YoloName(id, name);
            }

            return names;
        }

        #endregion
    }
    public readonly struct KeypointShape(int count, int channels)
    {
        public int Count { get; } = count;

        public int Channels { get; } = channels;
    }
    public class YoloPoseMetadata : YoloMetadata
    {
        public KeypointShape KeypointShape { get; }

        internal YoloPoseMetadata(InferenceSession session) : base(session)
        {
            var metadata = session.ModelMetadata.CustomMetadataMap;

            KeypointShape = ParseKeypointShape(metadata["kpt_shape"]);
        }

        private static KeypointShape ParseKeypointShape(string text)
        {
            text = text[1..^1]; // '[17, 3]' => '17, 3'

            var split = text.Split(", ");

            var count = int.Parse(split[0]);
            var channels = int.Parse(split[1]);

            return new KeypointShape(count, channels);
        }
    }
    internal class NonMaxSuppressionService : INonMaxSuppressionService
    {
        public T[] Suppress<T>(Span<T> boxes, float iouThreshold) where T : IRawBoundingBox<T>
        {
            if (boxes.Length == 0)
            {
                return [];
            }

            // Sort by confidence from the high to the low 
            boxes.Sort((x, y) => y.CompareTo(x));

            // Initialize result with highest confidence box
            var result = new List<T>(4)
            {
                boxes[0]
            };

            // Iterate boxes (Skip with the first box because it already has been added)
            for (var i = 1; i < boxes.Length; i++)
            {
                var box1 = boxes[i];
                var addToResult = true;

                for (var j = 0; j < result.Count; j++)
                {
                    var box2 = result[j];

                    // Skip boxers with different label
                    if (box1.NameIndex != box2.NameIndex)
                    {
                        continue;
                    }

                    // If the box overlaps another box already in the results 
                    if (T.CalculateIoU(ref box1, ref box2) > iouThreshold)
                    {
                        addToResult = false;
                        break;
                    }
                }

                if (addToResult)
                {
                    result.Add(box1);
                }
            }

            return [.. result];
        }
    }
    internal interface INonMaxSuppressionService
    {
        public T[] Suppress<T>(Span<T> boxes, float iouThreshold) where T : IRawBoundingBox<T>;
    }
    internal class RawBoundingBoxParser(YoloMetadata metadata,
                                    YoloConfiguration configuration,
                                    INonMaxSuppressionService nonMaxSuppression) : IRawBoundingBoxParser
    {
        private T[] ParseYoloV8<T>(DenseTensor<float> tensor) where T : IRawBoundingBox<T>
        {
            var stride1 = tensor.Strides[1];
            var boxesCount = tensor.Dimensions[2];
            var namesCount = metadata.Names.Length;

            var boxes = MemoryPool<T>.Shared.Rent(boxesCount);
            var boxesIndex = 0;
            var boxesSpan = boxes.Memory.Span;
            var tensorSpan = tensor.Buffer.Span;

            var context = new RawParsingContext
            {
                Architecture = YoloArchitecture.YoloV8,
                Tensor = tensor,
                Stride1 = stride1,
                NameCount = namesCount,
            };

            for (var boxIndex = 0; boxIndex < boxesCount; boxIndex++)
            {
                for (var nameIndex = 0; nameIndex < namesCount; nameIndex++)
                {
                    var confidence = tensorSpan[(nameIndex + 4) * stride1 + boxIndex];

                    if (confidence <= configuration.Confidence)
                    {
                        continue;
                    }

                    var box = T.Parse(ref context, boxIndex, nameIndex, confidence);

                    if (box.Bounds.Width == 0 || box.Bounds.Height == 0)
                    {
                        continue;
                    }

                    boxesSpan[boxesIndex++] = box;
                }
            }

            return nonMaxSuppression.Suppress(boxesSpan[..boxesIndex], configuration.IoU);
        }

        private T[] ParseYoloV10<T>(DenseTensor<float> tensor) where T : IRawBoundingBox<T>
        {
            var stride1 = tensor.Strides[1];
            var stride2 = tensor.Strides[2];

            var boxesCount = tensor.Dimensions[1];
            var boxes = MemoryPool<T>.Shared.Rent(boxesCount);
            var boxesIndex = 0;
            var boxesSpan = boxes.Memory.Span;
            var tensorSpan = tensor.Buffer.Span;

            var context = new RawParsingContext
            {
                Architecture = YoloArchitecture.YoloV10,
                Tensor = tensor,
                Stride1 = stride1
            };

            for (var index = 0; index < boxesCount; index++)
            {
                var boxOffset = index * stride1;

                var confidence = tensorSpan[boxOffset + 4 * stride2];

                if (confidence <= configuration.Confidence)
                {
                    continue;
                }

                var nameIndex = (int)tensorSpan[boxOffset + 5 * stride2];
                var box = T.Parse(ref context, index, nameIndex, confidence);

                if (box.Bounds.Width == 0 || box.Bounds.Height == 0)
                {
                    continue;
                }

                boxesSpan[boxesIndex++] = box;
            }

            return nonMaxSuppression.Suppress(boxesSpan[..boxesIndex], configuration.IoU);
        }

        public T[] Parse<T>(DenseTensor<float> tensor) where T : IRawBoundingBox<T>
        {
            if (metadata.Architecture == YoloArchitecture.YoloV10)
            {
                return ParseYoloV10<T>(tensor);
            }

            return ParseYoloV8<T>(tensor);
        }
    }

    internal struct RawBoundingBox : IRawBoundingBox<RawBoundingBox>
    {
        public required int Index { get; init; }

        public required int NameIndex { get; init; }

        public required RectangleF Bounds { get; set; }

        public required float Confidence { get; init; }

        public static float CalculateIoU(ref RawBoundingBox box1, ref RawBoundingBox box2)
        {
            var rect1 = box1.Bounds;
            var rect2 = box2.Bounds;

            var area1 = rect1.Width * rect1.Height;

            if (area1 <= 0f)
            {
                return 0f;
            }

            var area2 = rect2.Width * rect2.Height;

            if (area2 <= 0f)
            {
                return 0f;
            }

            var intersection = RectangleF.Intersect(rect1, rect2);
            var intersectionArea = intersection.Width * intersection.Height;

            return (float)intersectionArea / (area1 + area2 - intersectionArea);
        }

        public static RawBoundingBox Parse(ref RawParsingContext context, int index, int nameIndex, float confidence)
        {
            var tensor = context.Tensor;
            var tensorSpan = tensor.Buffer.Span;
            var stride1 = context.Stride1;

            RectangleF bounds;

            if (context.Architecture == YoloArchitecture.YoloV10)
            {
                var boxOffset = index * stride1;

                var xMin = (int)tensorSpan[boxOffset + 0];
                var yMin = (int)tensorSpan[boxOffset + 1];
                var xMax = (int)tensorSpan[boxOffset + 2];
                var yMax = (int)tensorSpan[boxOffset + 3];

                bounds = new RectangleF(xMin, yMin, xMax - xMin, yMax - yMin);
            }
            else // YOLOv8
            {
                var x = tensorSpan[0 + index];
                var y = tensorSpan[1 * stride1 + index];
                var w = tensorSpan[2 * stride1 + index];
                var h = tensorSpan[3 * stride1 + index];

                bounds = new RectangleF(x - w / 2, y - h / 2, w, h);
            }

            return new RawBoundingBox
            {
                Index = index,
                Bounds = bounds,
                NameIndex = nameIndex,
                Confidence = confidence,
            };
        }

        public readonly int CompareTo(RawBoundingBox other) => Confidence.CompareTo(other.Confidence);
    }
    internal unsafe class SegmentationParser(YoloMetadata metadata, 
        IRawBoundingBoxParser rawBoundingBoxParser) 
        : IParser<Segmentation>
    {
        public Segmentation[] ProcessTensorToResult(YoloRawOutput output, 
            Rectangle interestRegion, float threshold)
        {
            var output0 = output.Output0;
            var output1 = output.Output1 ?? throw new Exception();

            var boxes = rawBoundingBoxParser.Parse<RawBoundingBox>(output0);
            var maskChannelCount = output0.Dimensions[1] - 4 - metadata.Names.Length;

            var result = new Segmentation[boxes.Length];

            for (var index = 0; index < boxes.Length; index++)
            {
                var box = boxes[index];
                var bounds = box.Bounds.TransformBy(metadata.ImageSize, interestRegion);

                using var maskWeights = CollectMaskWeights(output0, box.Index, maskChannelCount, metadata.Names.Length + 4);

                var mask = ProcessMask(output1, maskWeights.Memory.Span);

                result[index] = new Segmentation
                {
                    Mask = mask,
                    Name = metadata.Names[box.NameIndex],
                    Bounds = bounds,
                    Confidence = box.Confidence,
                    InterestRegion = interestRegion,
                    Threshold = threshold
                };
            }

            return result;
        }

        private static Mat ProcessMask(Tensor<float> prototypes,
                                                    ReadOnlySpan<float> weights)
        {
            var maskChannels = prototypes.Dimensions[1];
            var maskHeight = prototypes.Dimensions[2];
            var maskWidth = prototypes.Dimensions[3];

            if (maskChannels != weights.Length)
                throw new InvalidOperationException();

            var size = new Size(maskWidth, maskHeight);
            var bitmap = new Mat(size, DepthType.Cv8U, 1);
            var data = (byte*)bitmap.DataPointer;
            for (var y = 0; y < maskHeight; y++)
            {
                for (var x = 0; x < maskWidth; x++)
                {
                    var value = 0F;
                    for (int i = 0; i < maskChannels; i++) 
                        value += prototypes[0, i, y, x] * weights[i];
                    
                    value = value.Sigmoid();
                    var color = 255-value.GetLuminance();
                    
                    data[y * maskWidth + x] = (byte)color;
                }
            }

            return bitmap;
        }

        private IMemoryOwner<float> CollectMaskWeights(Tensor<float> output, int boxIndex, int maskChannelCount, int maskWeightsOffset)
        {
            var weights = MemoryPool<float>.Shared.Rent(maskChannelCount);
            var weightsSpan = weights.Memory.Span;

            for (int i = 0; i < maskChannelCount; i++)
            {
                weightsSpan[i] = output[0, maskWeightsOffset + i, boxIndex];
            }

            return weights;
        }

        

      

        
    }
}
