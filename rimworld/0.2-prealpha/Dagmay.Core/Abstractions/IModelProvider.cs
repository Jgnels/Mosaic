using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Reflection;

namespace Dagmay.Core.Abstractions
{
    public interface IModelProvider
    {
        string ProviderId { get; }

        ProviderCapabilities Capabilities { get; }

        Task<ModelResult> GenerateStructuredAsync(ModelRequest request, CancellationToken cancellationToken);
    }
}

