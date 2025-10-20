using BlueArchiveAPI.NetworkModels;
using System.Text;

namespace BlueArchiveAPI.Handlers
{
    public class Queuing : BaseHandler<QueuingGetTicketRequest, QueuingGetTicketResponse>
    {
        protected override async Task<QueuingGetTicketResponse> Handle(QueuingGetTicketRequest request)
        {
            byte[] rawTicketBytes = Encoding.UTF8.GetBytes($"{request.NpSN}/{request.NpToken}");
            
            return new QueuingGetTicketResponse
            {
                EnterTicket = Convert.ToBase64String(rawTicketBytes)
            };
        }
    }
}
