using MicroPlumberd;
using ModelingEvolution.VideoStreaming.Recordings.Segmentation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming.Recordings
{
    [EventHandler]
    public partial class SegmentationClassModel
    {
        private readonly ConcurrentDictionary<Guid, SegmentationClass> _byId = new();
        private async Task Given(Metadata m, SegmentationClassAdded ev)
        {

        }

    }
}
