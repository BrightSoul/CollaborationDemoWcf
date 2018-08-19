using CollaborationDemo.WCF;
using CollaborationDemo.WCF.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CollaborationDemo
{
    class Program
    {
        private static CancellationTokenSource listeningCancellationTokenSource = new CancellationTokenSource();
        private static CancellationTokenSource workCancellationTokenSource = null;
        private static IPAddress localAddress;
        static async Task Main(string[] args)
        {
            //Get configuration
            var networkAddress = NetworkAddress.Parse(ConfigurationManager.AppSettings["Network"]);
            var broadcastPort = int.Parse(ConfigurationManager.AppSettings["BroadcastUdpPort"]);
            var unicastPort = int.Parse(ConfigurationManager.AppSettings["UnicastTcpPort"]);
            localAddress = networkAddress.GetLocalAddress();

            //This client exposes all of the high-level functionality
            var collaborationClient = new CollaborationClient(networkAddress, broadcastPort, unicastPort);
            collaborationClient.CollaborationRequestAccepted += OnCollaborationRequestAccepted;
            collaborationClient.CollaborationRequestDeclined += OnCollaborationRequestDeclined;
            collaborationClient.StopCollaborationRequested += OnStopCollaborationRequested;
            collaborationClient.SubscriptionRequested += OnSubscriptionRequested;
            collaborationClient.SubscriberNotifiedToStopCollaborating += OnSubscriberNotifiedToStopCollaborating;

            //Start listening per collaboration requests broadcasts
            var broadcastListenTask = StartListeningForCollaborationRequests(collaborationClient, listeningCancellationTokenSource.Token);
            //And also listen for subscription requests
            var subscriptionListenTask = StartListeningForSubscriptions(collaborationClient, listeningCancellationTokenSource.Token);
            //Then, read commands from the user
            HandleUserInput(collaborationClient);

            //When it's time to close the app, cancel any pending work and shutdown the hosts
            workCancellationTokenSource?.Cancel();
            await Task.WhenAll(broadcastListenTask, subscriptionListenTask);
        }

        private static void HandleUserInput(CollaborationClient client)
        {
            ConsoleKeyInfo input;
            WriteInstructions();

            while ((input = Console.ReadKey()).Key != ConsoleKey.Enter)
            {
                Console.WriteLine();
                switch (input.Key)
                {
                    case ConsoleKey.R:
                        //Let's generate some random filename
                        //TODO: this needs to be modified with appropriate logic
                        var filename = Guid.NewGuid().ToString().Split('-').Last() + ".ext";
                        try
                        {
                            var collaboration = client.BroadcastCollaborationRequestForFile(filename);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Collaboration request broadcast successfully for file {collaboration.Filename}, starting work now...");
                            Console.ResetColor();
                            workCancellationTokenSource = new CancellationTokenSource();
                            PerformWorkOnFile(collaboration.Filename, workCancellationTokenSource.Token);
                        } catch (Exception exc)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(exc.Message);
                            Console.ResetColor();
                        }
                        break;

                    case ConsoleKey.S:
                        workCancellationTokenSource?.Cancel();
                        client.StopCollaboration();
                        Console.WriteLine("Work stopped");
                        WriteInstructions();
                        break;
                }
            }
            listeningCancellationTokenSource.Cancel();
        }

        private static void WriteInstructions()
        {
            Console.WriteLine();
            Console.Write("Press R to broadcast a collaboration request or Enter to quit: ");
        }

        private static async Task StartListeningForCollaborationRequests(CollaborationClient client, CancellationToken token)
        {
            Uri uri = client.ListenForCollaborationRequests();
            Console.WriteLine($"Started listening on incoming collaboration requests on address {uri}");
            try
            {
                await Task.Delay(-1, token);
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                client.StopListeningForCollaborationRequests();
            }
        }

        private static async Task StartListeningForSubscriptions(CollaborationClient client, CancellationToken token)
        {
            Uri uri = client.ListenForSubscriptionRequests();
            Console.WriteLine($"Started listening on incoming subscription requests on address {uri}");
            try
            {
                await Task.Delay(-1, token);
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                client.StopListeningForSubscriptions();
            }
        }

        private static void PerformWorkOnFile(string filename, CancellationToken token)
        {
            Task.Run(async () =>
            {
                //TODO: this is dummy work, change this with proper logic
                while (true)
                {
                    try
                    {
                        await Task.Delay(2000, token);
                        Console.WriteLine($"Doing work with file {filename}, stop by pressing S...");
                    } catch
                    {
                        break;
                    }
                }
            }, token);
        }

        #region event handlers
        private static void OnCollaborationRequestAccepted(object sender, CollaborationRequest request)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"A collaboration request arrived from {request.SenderName} ({request.SenderAddress}) for file {request.Filename}, starting work now...");
            Console.ResetColor();
            //TODO: this call should be made only if this application actually wants to collaborate
            //In this example, let's collaborate only if we're not working on a file ourselves
            workCancellationTokenSource = new CancellationTokenSource();
            PerformWorkOnFile(request.Filename, workCancellationTokenSource.Token);

        }
        private static void OnCollaborationRequestDeclined(object sender, CollaborationRequest request)
        {
            Console.WriteLine($"A collaboration request arrived from {request.SenderName} ({request.SenderAddress}), but it was declined since we're already busy");
        }

        private static void OnSubscriberNotifiedToStopCollaborating(object sender, Subscriber subscriber)
        {
            Console.WriteLine($"Subscriber {subscriber.Name} ({subscriber.Address}) was notified to stop collaborating");
        }

        private static void OnSubscriptionRequested(object sender, Subscriber subscriber)
        {
            Console.WriteLine($"Subscriber {subscriber.Name} ({subscriber.Address}) is now collaborating");
        }

        private static void OnStopCollaborationRequested(object sender, CollaborationRequest request)
        {
            Console.WriteLine($"We were asked to stop our collaboration on file {request.Filename}, stopping work now...");
            workCancellationTokenSource?.Cancel();
            WriteInstructions();
        }

        #endregion
    }
}
