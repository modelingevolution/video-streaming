using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming.Ui.Components
{
    internal static class RecordingButtonExtensions
    {
        public static string Style(this VideoRecordingDeviceModel.State st)
        {
            return st.IsRecording ? "fill:red" : "fill:gray";
        }
    }
}
