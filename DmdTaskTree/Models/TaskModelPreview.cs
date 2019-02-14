using DmdTaskTree.DataAccessLayer;
using System.Collections.Generic;

namespace DmdTaskTree.Models
{
    public class TaskModelPreview
    {
        public TaskModelPreview(TaskNote taskNote)
        {
            Id = taskNote.Id;
            Name = taskNote.Name;;
            Status = taskNote.Status;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
        public Statuses Status { get; private set; }
        public TaskModelPreview[] Descendants { get; set; }
    }
}
