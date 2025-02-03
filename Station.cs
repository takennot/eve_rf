using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EVE_RF
{
    internal class Station
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("office_rental_cost")]
        public double OfficeRentalCost { get; set; }

        [JsonProperty("services")]
        public List<string> StationServices { get; set; }

        [JsonProperty("station_id")]
        public int StationID { get; set; }

        [JsonProperty("system_id")]
        public int SystemID { get; set; }

        [JsonProperty("type_id")]
        public int TypeID { get; set; }
    }
}
