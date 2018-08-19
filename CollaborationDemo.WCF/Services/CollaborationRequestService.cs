using CollaborationDemo.WCF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace CollaborationDemo.WCF.Services
{
    
    [ServiceContract]
    interface ICollaborationRequestService
    {
        [OperationContract(IsOneWay = true)]
        void BroadcastCollaborationRequest(CollaborationRequest request);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class CollaborationRequestService : ICollaborationRequestService
    {
        private readonly CollaborationClient client;
        public CollaborationRequestService(CollaborationClient client)
        {
            this.client = client;
        }

        public void BroadcastCollaborationRequest(CollaborationRequest request)
        {
            request.SenderAddress = GetClientIp();
            client.HandleCollaborationRequest(request);
        }

        private static IPAddress GetClientIp()
        {
            //https://stackoverflow.com/questions/33166679/get-client-ip-address-using-wcf-4-5-remoteendpointmessageproperty-in-load-balanc
            OperationContext context = OperationContext.Current;
            MessageProperties properties = context.IncomingMessageProperties;
            RemoteEndpointMessageProperty endpoint = properties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
            string address = string.Empty;
            if (properties.Keys.Contains(HttpRequestMessageProperty.Name))
            {
                HttpRequestMessageProperty endpointLoadBalancer = properties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;
                if (endpointLoadBalancer != null && endpointLoadBalancer.Headers["X-Forwarded-For"] != null)
                    address = endpointLoadBalancer.Headers["X-Forwarded-For"];
            }
            if (string.IsNullOrEmpty(address))
            {
                address = endpoint.Address;
            }
            return IPAddress.Parse(address);
        }
    }
}
