using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CollaborationDemo.WCF.Models
{
    [DataContract]
    public class CollaborationRequest
    {

        [DataMember]
        public string Filename { get; set; }

        [DataMember]
        public string SenderName { get; set; }

        [IgnoreDataMember]
        public IPAddress SenderAddress { get; set; }

        public override int GetHashCode()
        {
            return (Filename ?? "").GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as CollaborationRequest;
            if (other == null)
            {
                return false;
            }
            return other.Filename == this.Filename;
        }
    }
}
