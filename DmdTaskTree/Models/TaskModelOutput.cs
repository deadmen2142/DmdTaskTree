using DmdTaskTree.DataAccessLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DmdTaskTree.Models
{
    public class TaskModelOutput
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Performers { get; set; }
        public Statuses Status { get; set; }
        public string CreationDate { get; set; }
        public string FinishDate { get; set; }

        public string CalculatedPlanedExecutionTime { get; set; }

        public string ExecutionTime { get; set; }
        public string SubtaskExecutionTime { get; set; }
    }
}
