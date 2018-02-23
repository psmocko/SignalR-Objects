using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Interfaces
{
    public interface IObservationDeckSignalRProxy
    {
        bool IsConnected { get; }
        Task SendDeskEventAddedMessage(DeskClientEvent model);
        Task SendDeskEventUpdatedMessage(IEnumerable<DeskClientEvent> model);
    }
}
