using DmdTaskTree.DataAccessLayer;
using DmdTaskTree.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DmdTaskTree.Tests
{
    public static class TestHelper
    {
        public static void ClearDatabase(DbContextOptions<TaskContext> options)
        {
            using (TaskContext db = new TaskContext(options))
            {
                List<TaskNote> taskNotes = db.TaskNotes.ToList();
                foreach (TaskNote note in taskNotes)
                {
                    db.TaskNotes.Remove(note);
                }

                List<TaskTreeNode> taskNodes = db.TaskTreeNodes.ToList();
                foreach (TaskTreeNode node in taskNodes)
                {
                    db.TaskTreeNodes.Remove(node);
                }
                db.SaveChanges();
            }
        }

        public static void SetStatus(TaskManager manager, TaskNote[] tasks, Statuses status)
        {
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i].Status = status;
                manager.Update(tasks[i]);
            }
        }

        public static void Refresh(TaskManager manager, TaskNote[] tasks)
        {
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = manager.Find(tasks[i].Id);
            }
        }
    }
}