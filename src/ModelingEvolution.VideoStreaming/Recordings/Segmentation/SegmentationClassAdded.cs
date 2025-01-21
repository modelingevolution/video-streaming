﻿using MicroPlumberd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming.Recordings.Segmentation
{
    [OutputStream("SegmentationClass")]
    public class SegmentationClassAdded
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }

    }
}
