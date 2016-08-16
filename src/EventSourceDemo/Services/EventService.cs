using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventSourceDemo.Services
{
    public class EventsService {
        private readonly object collectionsLock = new object();
        // This dictionary links event names to a list of subscribers to that event
        private Dictionary<string, List<EventSubscription>> eventSubscriptionList = new Dictionary<string, List<EventSubscription>>();

        // this dictionary links subscribers to the lists that they are subscribed to,
        // for quick removal during unsubscribe
        private Dictionary<EventSubscription, List<List<EventSubscription>>> subscriptionMembershipLists = new Dictionary<EventSubscription, List<List<EventSubscription>>>();

        public void Notify(string eventName, object data) {
            lock (collectionsLock) {
                List<EventSubscription> subscriptions = null;
                if (eventSubscriptionList.TryGetValue(eventName, out subscriptions)) {
                    // notify all subscriptions that this event has occurred
                    NotifyList(subscriptions, data);
                }
            }
        }

        public void NotifyWhere(Func<string, bool> predicate, object data) {
            lock (collectionsLock) {
                List<EventSubscription> subscriptions = eventSubscriptionList.Where(kvp => predicate(kvp.Key))
                    .SelectMany(kvp => kvp.Value)
                    .Distinct()
                    .ToList();

                NotifyList(subscriptions, data);
            }
        }

        private void NotifyList(List<EventSubscription> subscriptions, object data) {
            foreach (EventSubscription thisSubscription in subscriptions) {
                thisSubscription.Notify(data);
            }
        }

        public EventSubscription<T> SubscribeTo<T>(params string[] eventNames) where T : class {
            if (eventNames == null) throw new ArgumentNullException(nameof(eventNames));
            EventSubscription<T> subscription = new EventSubscription<T>(this);

            lock (collectionsLock) {
                List<List<EventSubscription>> eventMemberships = new List<List<EventSubscription>>();
                subscriptionMembershipLists.Add(subscription, eventMemberships);

                foreach (string thisEventName in eventNames) {
                    List<EventSubscription> subscriptions = null;
                    if (!eventSubscriptionList.TryGetValue(thisEventName, out subscriptions)) {
                        // add the list against this event name if it doesn't exist
                        subscriptions = new List<EventSubscription>();
                        eventSubscriptionList.Add(thisEventName, subscriptions);
                    }

                    // add the subscription to this subscriptions list
                    subscriptions.Add(subscription);

                    // add this subscriptions list to the list of event memberships that this is part of.
                    eventMemberships.Add(subscriptions);
                }
            }

            return subscription;
        }

        public void Unsubscribe(EventSubscription subscription) {
            lock (collectionsLock) {
                List<List<EventSubscription>> subscriptionsList;
                if (subscriptionMembershipLists.TryGetValue(subscription, out subscriptionsList)) {
                    foreach (List<EventSubscription> subscriptions in subscriptionsList) {
                        lock (subscriptions) {
                            subscriptions.Remove(subscription);
                        }
                    }

                    subscriptionMembershipLists.Remove(subscription);
                }
            }
        }
    }

    public class DataReceivedEventArgs<T> : EventArgs {
        public DataReceivedEventArgs(T data) {

        }
        public T Data { get; }
    }

    public abstract class EventSubscription : IDisposable {
        public abstract void Dispose();

        public abstract void Notify(object data);
    }

    public class EventSubscription<T> : EventSubscription where T : class {
        public event EventHandler<DataReceivedEventArgs<T>> DataReceived;
        private EventsService service;
        private readonly object deliveryQueueLock = new object();
        private Queue<T> deliveryQueue = new Queue<T>();
        private SemaphoreSlim signal = new SemaphoreSlim(0, int.MaxValue);

        public EventSubscription(EventsService service) {
            this.service = service;
        }

        public override void Notify(object data) {
            T genericData = data as T;

            if (genericData != null) {
                Notify(genericData);
            }
        }

        public void Notify(T data) {
            // add data to the queue for consumption
            lock (deliveryQueueLock) {
                deliveryQueue.Enqueue(data);
            }

            signal.Release();

            // fire event occured event with data
            DataReceived?.Invoke(this, new DataReceivedEventArgs<T>(data));
        }

        public async Task<T> WaitForData() {
            await signal.WaitAsync();

            lock (deliveryQueueLock) {
                return deliveryQueue.Dequeue();
            }
        }

        public override void Dispose() {
            service.Unsubscribe(this);
            signal.Dispose();
        }
    }
}
