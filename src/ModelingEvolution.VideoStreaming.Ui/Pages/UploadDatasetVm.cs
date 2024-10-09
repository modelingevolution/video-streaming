using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MicroPlumberd;
using Microsoft.AspNetCore.Components;
using ModelingEvolution.VideoStreaming.CVat;
using ModelingEvolution.VideoStreaming.Recordings;

namespace ModelingEvolution.VideoStreaming.Ui.Pages
{
    internal class UploadDatasetVm(ICommandBus bus, DatasetRecordingsModel model, VideoImgFrameProvider imgProvider, NavigationManager nm)
    {
        private DatasetRecording? _dataset;
        private FramesJson _frames;
        public string Error { get; private set; }
        public PublishDatasetRecording Command { get; private set; } = new PublishDatasetRecording();
        public DatasetRecording Data => _dataset;
        public int FrameCount
        {
            get => Command.CalculateSet(_frames.Keys).Count();
        }

        public void Init(Guid id)
        {
            this._dataset = model.GetById(id);
            _frames = imgProvider[_dataset.DirectoryName];
            OnCreateNewCommand(_frames);
        }

        private void OnCreateNewCommand(FramesJson frames)
        {
            Command = new PublishDatasetRecording()
            {
                StartFrame = frames.Count > 0 ? frames.Keys.First() : 0,
                EndFrame = frames.Count > 0 ? frames.Keys.Last() : ulong.MaxValue,
                Every = 1,
                Name = _dataset.Name
            };
        }

        public async Task Publish()
        {
            try
            {
                Error = null;
                await bus.SendAsync(_dataset.Id, Command);

                OnCreateNewCommand(_frames);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }
}
