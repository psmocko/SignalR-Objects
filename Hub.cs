using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace API
{
    [HubName("ObservationDeckHub")]
    public class ObservationDeckHub : Hub
    {
        public async Task SendDeskEventAddedMessage(DeskClientEvent model)
        {
            await Clients.All.Invoke("DeskEventAddedMessage", model);
        }

        public async Task SendDeskEventUpdatedMessage(IList<DeskClientEvent> model)
        {
            await Clients.All.Invoke("DeskEventUpdatedMessage", model);
        }
    }
}