using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventSourceDemo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventSourceDemo.Controllers {
    public class HomeController : Controller {

        public HomeController(EventsService eventsService,
            ILogger<HomeController> logger) {
            // dependency inject the event service and logger
            this.eventsService = eventsService;
            this.logger = logger;

            // create a singleton timer in a slightly hacky fashion
            // ideally this should be handled by some sort of timer service
            // that gets pushed into here with DI
            lock (heartbeatTimerLock) {
                if (heartbeatTimer == null) {
                    heartbeatTimer = new Timer(new TimerCallback(HeartbeatTimerTick), null, 1000, 1000);
                }
            }
        }

        private EventsService eventsService;
        private ILogger<HomeController> logger;

        private static readonly object heartbeatTimerLock = new object();
        private volatile static Timer heartbeatTimer = null;

        [HttpGet]
        public IActionResult Index() {
            return View();
        }

        private void HeartbeatTimerTick(object state) {
            // every time the timer fires, it will raise a heartbeat event
            // with an event source update that contains the current time
            // as a string
            string currentTimeString = DateTimeOffset.Now.ToString("o");

            eventsService.Notify("heartbeat", new EventSourceUpdate() {
                Comment = $"heartbeat {currentTimeString}",
                Event = "Message",
                DataObject = new { Message = currentTimeString }
            });
        }

        [HttpGet]
        public async Task<string> EventSource() {
            // use the event stream content type, as per specification
            HttpContext.Response.ContentType = "text/event-stream";

            // get the last event id out of the header
            string lastEventIdString = HttpContext.Request.Headers["Last-Event-ID"].FirstOrDefault();
            int temp;
            int? lastEventId = null;

            if (lastEventIdString != null && int.TryParse(lastEventIdString, out temp)) {
                lastEventId = temp;
            }

            string remoteIp = HttpContext.Connection.RemoteIpAddress.ToString();


            // open the current request stream for writing.
            // Use UTF-8 encoding, and do not close the stream when disposing.
            using (var clientStream = new StreamWriter(HttpContext.Response.Body, Encoding.UTF8, 1024, true) { AutoFlush = true }) {

                // subscribe to the heartbeat event. Elsewhere, a timer will push updates to this event periodically.
                using (EventSubscription<EventSourceUpdate> subscription = eventsService.SubscribeTo<EventSourceUpdate>("heartbeat")) {
                    try {
                        logger.LogInformation($"Opened event source stream to address: {remoteIp}");

                        await clientStream.WriteLineAsync($":connected {DateTimeOffset.Now.ToString("o")}");

                        // If a last event id is given, pump out any intermediate events here.
                        if (lastEventId != null) {
                            // We're not doing anything that stores events in this heartbeat demo,
                            // so do nothing here.
                        }

                        // start pumping out events as they are pushed to the subscription queue
                        while (true) {
                            // asynchronously wait for an event to fire before continuing this loop.
                            // this is implemented with a semaphore slim using the async wait, so it
                            // should play nice with the async framework.
                            EventSourceUpdate update = await subscription.WaitForData();

                            // push the update down the request stream to the client
                            if (update != null) {
                                string updateString = update.ToString();
                                await clientStream.WriteAsync(updateString);
                            }
                        }
                    }
                    catch (Exception e) {
                        // catch client closing the connection
                        logger.LogInformation($"Closed event source stream from {remoteIp}. Message: {e.Message}");
                    }
                }
            }

            return ":closed";
        }

        public IActionResult Contact() {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error() {
            return View();
        }
    }
}
