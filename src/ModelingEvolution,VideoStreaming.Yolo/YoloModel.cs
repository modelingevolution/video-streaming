using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using Emgu.CV.Dnn;
using Microsoft.Extensions.Options;

namespace ModelingEvolution_VideoStreaming.Yolo
{
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
}
