using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitterStatistics.Models {
    public class Add {
        public string value { get; set; }
        public string tag { get; set; }
    }

    public class Rules {
        public List<Add>? add { get; set; }
    }
}
