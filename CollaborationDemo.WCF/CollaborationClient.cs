using CollaborationDemo.WCF.Models;
using CollaborationDemo.WCF.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CollaborationDemo.WCF
{
    public class CollaborationClient
    {
        private readonly NetworkAddress networkAddress;
        private readonly int broadcastPort;
        private readonly int unicastPort;
        private CollaborationRequest currentCollaborationRequest;
        private static object collaborationRequestLock = new object();
        private readonly ConcurrentDictionary<CollaborationRequest, ConcurrentBag<Subscriber>> subscribers = new ConcurrentDictionary<CollaborationRequest, ConcurrentBag<Subscriber>>();

        public event EventHandler<CollaborationRequest> CollaborationRequestAccepted;
        public event EventHandler<CollaborationRequest> CollaborationRequestDeclined;
        public event EventHandler<CollaborationRequest> StopCollaborationRequested;

        public event EventHandler<Subscriber> SubscriptionRequested;
        public event EventHandler<Subscriber> SubscriberNotifiedToStopCollaborating;

        public CollaborationClient(NetworkAddress networkAddress, int broadcastPort, int unicastPort)
        {
            this.networkAddress = networkAddress;
            this.broadcastPort = broadcastPort;
            this.unicastPort = unicastPort;
        }

        public CollaborationRequest CurrentCollaborationRequest
        {
            get
            {
                lock(collaborationRequestLock)
                {
                    return currentCollaborationRequest;
                }
            }
        }

        private bool SwitchCollaborationRequest(CollaborationRequest request)
        {
            lock(collaborationRequestLock)
            {
                if (currentCollaborationRequest != null)
                {
                    return false;
                }
                currentCollaborationRequest = request;
                return true;
            }
        }

        public CollaborationRequest BroadcastCollaborationRequestForFile(string filename)
        {
            var collaborationRequest = CreateRequestForFilename(filename);
            if (!SwitchCollaborationRequest(collaborationRequest))
            {
                throw new InvalidOperationException("Cannot broadcast a collaboration request now: we're already working on another collaboration");
            }

            //Create a WCF client by indicating A-B-C: Address, Binding, Contract
            //Address
            string serviceAddress = $"soap.udp://{networkAddress.BroadcastAddress}:{broadcastPort}";
            //Binding
            UdpBinding myBinding = new UdpBinding();
            //Create the ChannelFactory by indicating the Contract (i.e. the service interface)
            ChannelFactory<ICollaborationRequestService> factory = new ChannelFactory<ICollaborationRequestService>(myBinding, new EndpointAddress(serviceAddress));

            //Open the channel and send the request
            ICollaborationRequestService proxy = factory.CreateChannel();
            proxy.BroadcastCollaborationRequest(collaborationRequest);
            factory.Close();
            return collaborationRequest;
        }

        public void StopCollaboration()
        {
            CollaborationRequest collaborationRequest;
            lock(collaborationRequestLock)
            {
                collaborationRequest = currentCollaborationRequest;
                currentCollaborationRequest = null;
            }
            if (collaborationRequest == null)
            {
                return;
            }
            var localAddress = networkAddress.GetLocalAddress();
            if (collaborationRequest.SenderAddress.Equals(localAddress))
            {
                NotifySubscribersTheyShouldStopCollaborating(collaborationRequest);
            }
        }

        private void NotifySubscribersTheyShouldStopCollaborating(CollaborationRequest collaborationRequest)
        {
            bool couldRemove = subscribers.TryRemove(collaborationRequest, out ConcurrentBag<Subscriber> subscribersList);
            if (!couldRemove)
            {
                return;
            }
            foreach (var subscriber in subscribersList)
            {
                NotifySubscriberToStopCollaborating(collaborationRequest, subscriber);
                SubscriberNotifiedToStopCollaborating?.Invoke(this, subscriber);
            }
        }

        private void SubscribeCollaborator(CollaborationRequest request)
        {
            //Create a WCF client by indicating A-B-C: Address, Binding, Contract
            //Address
            var serviceAddress = GetCollaborationServiceAddress(request.SenderAddress, unicastPort);
            //Binding
            NetTcpBinding tcpBinding = new NetTcpBinding();
            tcpBinding.Security.Mode = SecurityMode.None;
            tcpBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            //Create the ChannelFactory by indicating the Contract (i.e. the service interface)
            ChannelFactory<ISubscriptionService> factory = new ChannelFactory<ISubscriptionService>(tcpBinding, new EndpointAddress(serviceAddress));

            //Open the channel and send the request
            ISubscriptionService proxy = factory.CreateChannel();
            var subscriber = CreateSubscriber();
            proxy.SubscribeCollaborator(request, subscriber);
            factory.Close();
        }

        private void NotifySubscriberToStopCollaborating(CollaborationRequest request, Subscriber subscriber)
        {

            //Create a WCF client by indicating A-B-C: Address, Binding, Contract
            //Address
            var serviceAddress = GetCollaborationServiceAddress(subscriber.Address, unicastPort);
            //Binding
            NetTcpBinding tcpBinding = new NetTcpBinding();
            tcpBinding.Security.Mode = SecurityMode.None;
            tcpBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            //Create the ChannelFactory by indicating the Contract (i.e. the service interface)
            ChannelFactory<ISubscriptionService> factory = new ChannelFactory<ISubscriptionService>(tcpBinding, new EndpointAddress(serviceAddress));

            //Open the channel and send the request
            ISubscriptionService proxy = factory.CreateChannel();
            proxy.StopCollaborating(request);
            factory.Close();
        }

        private CollaborationRequest CreateRequestForFilename(string filename)
        {
            return new CollaborationRequest
            {
                Filename = filename,
                SenderName = $"{Environment.MachineName}",
                SenderAddress = networkAddress.GetLocalAddress()
            };
        }

        private Subscriber CreateSubscriber()
        {
            return new Subscriber
            {
                Name = $"{Environment.MachineName}",
                Address = networkAddress.GetLocalAddress()
            };
        }

        private static Uri GetCollaborationServiceAddress(IPAddress address, int port)
        {
            return new Uri($"net.tcp://{address}:{port}/");
        }

        #region handle requests received by WCF
        internal void HandleCollaborationRequest(CollaborationRequest request)
        {
            var localAddress = networkAddress.GetLocalAddress();
            if (request.SenderAddress.Equals(localAddress))
            {
                //It originated from myself, just discard it
                //When broadcasting, the sender also receives its own message
                return;
            }

            if (SwitchCollaborationRequest(request))
            {
                SubscribeCollaborator(request);
                CollaborationRequestAccepted?.Invoke(this, request);
            } else
            {
                CollaborationRequestDeclined?.Invoke(this, request);
            }
            
        }

        internal void HandleStopCollaboration(CollaborationRequest collaborationRequest)
        {
            StopCollaborationRequested?.Invoke(this, collaborationRequest);
            lock(collaborationRequestLock)
            {
                currentCollaborationRequest = null;
            }
        }

        internal void AddSubscriber(CollaborationRequest collaborationRequest, Subscriber subscriber)
        {
            var subscriberList = subscribers.GetOrAdd(collaborationRequest, request => new ConcurrentBag<Subscriber>());
            subscriberList.Add(subscriber);
            SubscriptionRequested?.Invoke(this, subscriber);
        }
        #endregion

        #region host management
        private ServiceHost collaborationRequestsHost;

        public Uri ListenForCollaborationRequests()
        {
            if (collaborationRequestsHost != null)
            {
                throw new InvalidOperationException("Already listening for collaboration requests");
            }
            var localAddress = networkAddress.GetLocalAddress();

            //Start listening for incoming collaboration requests
            var serviceAddress = new Uri($"soap.udp://{localAddress}:{broadcastPort}");
            UdpBinding udpBinding = new UdpBinding();
            collaborationRequestsHost = new ServiceHost(new CollaborationRequestService(this), serviceAddress);
            collaborationRequestsHost.AddServiceEndpoint(typeof(ICollaborationRequestService), udpBinding, string.Empty);
            collaborationRequestsHost.Open();
            return serviceAddress;

        }
        public void StopListeningForCollaborationRequests()
        {
            if (collaborationRequestsHost != null)
            {
                collaborationRequestsHost.Close();
                collaborationRequestsHost = null;
            }
        }

        private ServiceHost subscriptionRequestsHost;

        public Uri ListenForSubscriptionRequests()
        {
            if (subscriptionRequestsHost != null)
            {
                throw new InvalidOperationException("Already listening for collaboration requests");
            }
            var localAddress = networkAddress.GetLocalAddress();

            //Start listening for incoming collaboration requests
            var serviceAddress = GetCollaborationServiceAddress(localAddress, unicastPort);
            NetTcpBinding tcpBinding = new NetTcpBinding();
            tcpBinding.Security.Mode = SecurityMode.None;
            tcpBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            subscriptionRequestsHost = new ServiceHost(new SubscriptionService(this), serviceAddress);
            subscriptionRequestsHost.AddServiceEndpoint(typeof(ISubscriptionService), tcpBinding, string.Empty);
            subscriptionRequestsHost.Open();
            return serviceAddress;

        }
        public void StopListeningForSubscriptions()
        {
            if (subscriptionRequestsHost != null)
            {
                subscriptionRequestsHost.Close();
                subscriptionRequestsHost = null;
            }
        }
        #endregion
    }
}
