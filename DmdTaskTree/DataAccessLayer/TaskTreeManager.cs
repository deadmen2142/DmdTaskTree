using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DmdTaskTree.DataAccessLayer
{
    public class TaskTreeManager
    {
        protected readonly DbContextOptions<TaskContext> options;

        public TaskTreeManager(DbContextOptions<TaskContext> options)
        {
            this.options = options;
        }

        public TaskNote Find(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                return db.TaskNotes.Find(id);
            }
        }

        public virtual void Add(TaskNote task, TaskNote ancestor = null)
        {
            using (TaskContext db = new TaskContext(options))
            {
                if (db.TaskNotes.Find(task.Id) != null) throw new AddingException("Task already in database", task.Id);

                db.TaskNotes.Add(task);
                if (ancestor != null)
                {
                    if (db.TaskNotes.Find(ancestor.Id) == null) throw new NotFoundException("Parent is not found in database", ancestor.Id);
                    db.TaskTreeNodes.Add(new TaskTreeNode { AncestorId = ancestor.Id, DescendantId = task.Id });
                }
                db.SaveChanges();
            }
        }

        public virtual void Remove(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Include(t => t.TaskTreeNode).Include(t => t.TaskTreeNodes).Where(t => t.Id == id).FirstOrDefault();
                if (task == null) throw new NotFoundException("Task is not found in database", id);
                if (task.TaskTreeNodes.Count != 0) throw new NonTerminalException("Task is not terminal node", id);

                if (task.TaskTreeNode != null) db.TaskTreeNodes.Remove(task.TaskTreeNode);
                db.TaskNotes.Remove(task);
                db.SaveChanges();
            }
        }

        public virtual void Update(TaskNote task)
        {
            using (TaskContext db = new TaskContext(options))
            {
                db.TaskNotes.Update(task);
                db.SaveChanges();
            }
        }

        public bool IsTerminal(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Include(t => t.TaskTreeNodes).Where(t => t.Id == id).SingleOrDefault();
                if (task == null) throw new NotFoundException("Task is not found in database", id);

                return task.TaskTreeNodes.Count == 0;
            }
        }

        public bool HasDescendats(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Include(t => t.TaskTreeNodes).Where(t => t.Id == id).Single();
                if (task == null) throw new NotFoundException("Task is not found in database", id);

                return task.TaskTreeNodes.Count == 0 ? false : true;
            }
        }

        public List<TaskNote> GetDescendats(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Where(t => t.Id == id).SingleOrDefault();
                if (task == null) throw new NotFoundException("Task is not found in database", id);

                db.TaskTreeNodes.Where(ttn => ttn.AncestorId == task.Id).Include(ttn => ttn.Descendat).Load();

                List<TaskNote> taskNotes = new List<TaskNote>();
                if (task.TaskTreeNodes == null) return taskNotes;

                foreach (TaskTreeNode item in task.TaskTreeNodes)
                {
                    taskNotes.Add(item.Descendat);
                }

                return taskNotes;
            }
        }

        public TaskNote GetAncestor(int descendatId)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Where(t => t.Id == descendatId).FirstOrDefault();
                if (task == null) throw new NotFoundException("Task is not found in database", descendatId);

                db.TaskTreeNodes.Where(ttn => ttn.DescendantId == task.Id).Include(ttn => ttn.Ancestor).Load();

                return task.TaskTreeNode?.Ancestor;
            }
        }

        public List<TaskNote> GetRoots()
        {
            using (TaskContext db = new TaskContext(options))
            {
                return db.TaskNotes.Include(t => t.TaskTreeNode).Where(t => t.TaskTreeNode == null).ToList();
            }
        }
    }

    public class AddingException : InvalidOperationException
    {
        public int Value { get; }

        public AddingException(string message, int value) : base(message)
        {
            Value = value;
        }
    }

    public class NotFoundException : InvalidOperationException
    {
        public int Value { get; }

        public NotFoundException(string message, int value) : base(message)
        {
            Value = value;
        }
    }

    public class NonTerminalException : InvalidOperationException
    {
        public int Value { get; }

        public NonTerminalException(string message, int value) : base(message)
        {
            Value = value;
        }
    }
}