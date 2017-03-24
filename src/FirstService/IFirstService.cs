using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace FirstService
{
    public interface IFirstService : IService
    {
        Task DoStuffAsync(string unique, CancellationToken cancellationToken);
    }
}