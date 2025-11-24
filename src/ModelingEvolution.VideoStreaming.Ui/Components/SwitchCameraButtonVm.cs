using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming.Ui.Components
{
    internal class SwitchCameraButtonVm(
        VideoStreamingServer _srv, 
        NavigationManager _nm, 
        ILogger<SwitchCameraButtonVm> logger)
    {
        private int _currentCamera;
        public bool HasManyStreams => _srv.HasManySourceStreams;
        public void Switch()
        {
            try
            {
                _currentCamera = (_currentCamera + 1) % _srv.Streams.Count;

                var url = new Uri(_nm.Uri);

                var baseUrl = url.GetLeftPart(UriPartial.Path);
                var lastSlashIndex = baseUrl.LastIndexOf('/');
                var urlWithoutLastSegment = baseUrl.Substring(0, lastSlashIndex + 1);

                if (_currentCamera >= _srv.Streams.Count)
                {
                    logger.LogError("No camera available to switch to.");
                    return;
                }

                var newUrl = $"{urlWithoutLastSegment}{_srv.Streams[_currentCamera].VideoAddress.StreamName}";

                _nm.NavigateTo(newUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot change camera: " + ex.Message);
            }
        }
        
    }
}
