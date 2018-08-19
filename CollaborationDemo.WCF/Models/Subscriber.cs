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
    public class Subscriber
    {
        [DataMember]
        public string Name { get; set; }
        [IgnoreDataMember]
        public IPAddress Address { get; set; }
    }
}
