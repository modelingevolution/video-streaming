using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.VideoStreaming.VectorGraphics
{
    public static class ContainerExtensions
    {
        public static IServiceCollection AddVectorGraphicsStreaming(this IServiceCollection services)
        {
            services.AddTransient<StreamingCanvasEngine>();
            return services;
        }
    }
}
