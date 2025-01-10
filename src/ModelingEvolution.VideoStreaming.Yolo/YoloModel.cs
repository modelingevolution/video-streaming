using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Emgu.CV.Dnn;
using Microsoft.Extensions.Options;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution.VideoStreaming.Yolo
{
    public interface ISegmentationResult<out T> : IEnumerable<T>, IDisposable where T : IDisposable
    {
        FrameIdentifier Id { get; }
        Size DestinationSize { get; }
        int Count { get; }
        Rectangle Roi { get; }
        float Threshold { get; }
        T this[int index] { get; }
    }

    public class SegmentationResult<T>(T[] predictions) : ISegmentationResult<T> where T:IDisposable
        
    {
        public required Size ImageSize { get; init; }
        public required FrameIdentifier Id { get; init; }
        public required Size DestinationSize { get; init; }
        public T this[int index] => predictions[index];

        public int Count => predictions.Length;
        public required Rectangle Roi { get; init; }
        public required float Threshold { get; init; }
        public void Dispose()
        {
            for (int i = 0; i < predictions.Length; i++)
            {
                predictions[i]?.Dispose();
            }
        }

        #region Enumerator

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var item in predictions)
            {
                yield return item;
            }
        }


        #endregion

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
