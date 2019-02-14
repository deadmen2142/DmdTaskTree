using DmdTaskTree.DataAccessLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DmdTaskTree.Models
{
    public class TaskModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Performers { get; set; }
        public string Status { get; set; }
        public DateTime PlanedFinishDate { get; set; }
        public int Id { get; set; }
        public int AncestorId { get; set; }

        public long GetPlanedExecutionTime()
        {
            return DateTime.Now >= PlanedFinishDate ?
                    throw new WrongDateException("Finish date must be greater than current date") : (PlanedFinishDate - DateTime.Now).Ticks;
        }

        public Statuses GetStatus()
        {
            if (Status == Statuses.ToDo.ToString()) return Statuses.ToDo;
            if (Status == Statuses.InProgress.ToString()) return Statuses.InProgress;
            if (Status == Statuses.Pause.ToString()) return Statuses.Pause;
            if (Status == Statuses.Done.ToString()) return Statuses.Done;

            throw new InvalidOperationException("Uknown status: " + Status);
        }
    }



    public class WrongDateException : InvalidOperationException
    {
        public WrongDateException(string message) : base(message) { }
    }
}