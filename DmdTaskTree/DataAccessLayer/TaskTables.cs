using System;
using System.Collections.Generic;

namespace DmdTaskTree.DataAccessLayer
{
    public enum Statuses { ToDo = 0, InProgress, Pause, Done }

    public class TaskNote
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Performers { get; set; }
        public Statuses Status { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime FinishDate { get; set; }

        public long PlanedExecutionTime { get; set; }
        public long CalculatedPlanedExecutionTime { get; set; }

        public long ExecutionTime { get; set; }
        public DateTime StartExecutionDate { get; set; }
        public long CalculatedExecutionTime { get; set; }

        public List<TaskTreeNode> TaskTreeNodes { get; set; }
        public TaskTreeNode TaskTreeNode { get; set; }
    }

    public class TaskTreeNode
    {
        public int Id { get; set; }

        public int AncestorId { get; set; }
        public TaskNote Ancestor { get; set; }

        public int DescendantId { get; set; }
        public TaskNote Descendat { get; set; }
    }
}