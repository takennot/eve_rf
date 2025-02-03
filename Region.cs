using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EVE_RF;
internal class Region
{
    [JsonProperty("name")]
    public string Name { get; set; }
}
