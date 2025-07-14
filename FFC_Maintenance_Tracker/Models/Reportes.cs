using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FFC_Maintenance_Tracker.Models
{
    public class Reportes
    {
        public string folio { get; set; }
        public string line { get; set; }
        public string WhoCreate { get; set; }
        public string Date { get; set; }
        public string InternalModel { get; set; }
        public string PCBA { get; set; }
        public string Status { get; set; }
        public string Estacion { get; set; }

        public string PlacaMain { get; set; }

    }
}