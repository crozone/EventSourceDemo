using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EventSourceDemo.Services {
    public class EventSourceUpdate {
        public int? Id { get; set; }
        public string Event { get; set; }

        private object dataObject;
        public object DataObject
        {
            get { return this.dataObject; }
            set
            {
                this.dataObject = value;
                this.Data = JsonConvert.SerializeObject(value)
                    .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }
        }

        public List<string> Data { get; private set; }
        public string Comment { get; set; }

        public override string ToString() {
            string idStart = "id:";
            string eventStart = "event:";
            string dataStart = "data:";
            string commentStart = ":";

            StringBuilder sb = new StringBuilder();

            if (Comment != null) {
                sb.Append(commentStart);
                sb.AppendLine(Comment);
            }

            if (Event != null) {
                if (Id != null) {
                    sb.Append(idStart);
                    sb.AppendLine(Id.Value.ToString());
                }

                sb.Append(eventStart);
                sb.AppendLine(Event);

                if (Data != null) {
                    foreach (string thisDatum in Data) {
                        sb.Append(dataStart);
                        sb.AppendLine(thisDatum);
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static EventSourceUpdate FromObject(string eventName, object value, int? id = null) {
            return new EventSourceUpdate() {
                Id = id,
                Event = eventName,
                DataObject = value
            };
        }
    }
}
