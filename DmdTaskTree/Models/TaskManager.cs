using DmdTaskTree.DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DmdTaskTree.Models
{
    public class TaskManager : TaskTreeManager
    {
        public TaskManager(DbContextOptions<TaskContext> options) : base(options) { }


        public override void Add(TaskNote task, TaskNote ancestor = null)
        {
            Add(task: task, ancestorId: ancestor == null ? default(int) : ancestor.Id);
        }

        public void Add(TaskNote task, int ancestorId)
        {
            using (TaskContext db = new TaskContext(options))
            {
                ValidateTaskOnAdding(task);
                TaskNote ancestor = ValidateAncestor(ancestorId);

                OnAdd(task);
                db.TaskNotes.Add(task);
                db.SaveChanges();

                if (ancestor != null)
                {
                    ConnectDescendatToAncestor(task.Id, ancestor);
                    CalculatePlanedExecutionTimeForAncestor(ancestor, task.CalculatedPlanedExecutionTime, 0);
                }
            }
        }

        private void ValidateTaskOnAdding(TaskNote task)
        {
            using (TaskContext db = new TaskContext(options))
            {
                if (db.TaskNotes.Find(task.Id) != null)
                    throw new AddingException("Task already in database", task.Id);

                if (task.Status != Statuses.ToDo)
                    throw new StatusException("Cannot add task with status different from ToDo (Actual: " + task.Status.ToString() + ")");
            }
        }

        private TaskNote ValidateAncestor(int ancestorId)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote ancestor = null;
                if (ancestorId == default(int)) return ancestor;

                if ((ancestor = db.TaskNotes.Find(ancestorId)) == null)
                    throw new NotFoundException("Parent is not found in database", ancestorId);
                if (ancestor.Status == Statuses.Done)
                    throw new StatusException("Cannot add subtask to task with status Done");
                return ancestor;
            }
        }

        private void OnAdd(TaskNote task)
        {
            task.CreationDate = DateTime.Now;
            task.CalculatedPlanedExecutionTime = task.PlanedExecutionTime;
        }

        private void ConnectDescendatToAncestor(int descendatId, TaskNote ancestor)
        {
            using (TaskContext db = new TaskContext(options))
            {
                db.TaskTreeNodes.Add(new TaskTreeNode { AncestorId = ancestor.Id, DescendantId = descendatId });
                db.SaveChanges();
            }
        }

        public override void Remove(int id)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote task = db.TaskNotes.Include(t => t.TaskTreeNode).Where(t => t.Id == id).FirstOrDefault();
                if (task == null) throw new NotFoundException("Task is not found in database", id);

                if (db.TaskTreeNodes.Where(note => note.AncestorId == id).FirstOrDefault() != null)
                    throw new NonTerminalException("You can not remove non-terminal task", id);

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == id).SingleOrDefault()?.Ancestor;
                if (ancestor != null)
                {
                    CalculateExecutionTimeForAncestor(ancestor, 0, task.CalculatedExecutionTime);
                    CalculatePlanedExecutionTimeForAncestor(ancestor, 0, task.CalculatedPlanedExecutionTime);

                    db.TaskTreeNodes.Remove(task.TaskTreeNode);
                }

                db.TaskNotes.Remove(task);
                db.SaveChanges();
            }
        }

        public override void Update(TaskNote newTask)
        {
            using (TaskContext db = new TaskContext(options))
            {
                if (db.TaskNotes.Where(t => t.Id == newTask.Id).SingleOrDefault() == null)
                    throw new NotFoundException("Task is not found in database", newTask.Id);

                UpdateLogic(newTask);
            }
        }

        private void UpdateLogic(TaskNote newTask)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote curTask = db.TaskNotes.Where(t => t.Id == newTask.Id).SingleOrDefault();

                Commands command = ValidateStatusChange(curTask.Status, newTask.Status);
                if (command == Commands.Finish)
                {
                    List<TaskNote> subtasks = GetDescendants(newTask.Id);
                    foreach (TaskNote subtask in subtasks)
                        IsSubtasksCompletable(subtask);
                }

                // Recalculate values on status change
                ActionOnStatusChange(newTask, command);

                // Update calculated fields
                newTask.CalculatedPlanedExecutionTime = curTask.CalculatedPlanedExecutionTime - curTask.PlanedExecutionTime + newTask.PlanedExecutionTime;
                newTask.CalculatedExecutionTime = curTask.CalculatedExecutionTime - curTask.ExecutionTime + newTask.ExecutionTime;

                // Apply changes in ancestors
                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == newTask.Id).SingleOrDefault()?.Ancestor;
                if (ancestor != null)
                {
                    if (newTask.CalculatedPlanedExecutionTime != curTask.CalculatedPlanedExecutionTime)
                        CalculatePlanedExecutionTimeForAncestor(ancestor, newTask.CalculatedPlanedExecutionTime, curTask.CalculatedPlanedExecutionTime);

                    if (newTask.CalculatedExecutionTime != curTask.CalculatedExecutionTime)
                        CalculateExecutionTimeForAncestor(ancestor, newTask.CalculatedExecutionTime, curTask.CalculatedExecutionTime);
                }

                UpdateDb(newTask);

                // Subtask change status logic
                if (command == Commands.Finish)
                {
                    List<TaskNote> subtasks = GetDescendants(newTask.Id);
                    foreach (TaskNote subtask in subtasks)
                        SetSubtaskDoneStatus(subtask);
                } 
            }
        }

        private void UpdateDb(TaskNote task)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote curTask = db.TaskNotes.FirstOrDefault(t => t.Id == task.Id);
                db.Entry(curTask).CurrentValues.SetValues(task);
                db.SaveChanges();
            }
        }

        private void CalculatePlanedExecutionTimeForAncestor(TaskNote taskNote, long newCollected, long oldCollected)
        {
            using (TaskContext db = new TaskContext(options))
            {
                taskNote.CalculatedPlanedExecutionTime += newCollected - oldCollected;
                var old = db.TaskNotes.Find(taskNote.Id).CalculatedPlanedExecutionTime;
                UpdateDb(taskNote);

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == taskNote.Id).SingleOrDefault()?.Ancestor;
                if (ancestor == null) return;

                CalculatePlanedExecutionTimeForAncestor(ancestor, taskNote.CalculatedPlanedExecutionTime, old);
            }
        }

        private void CalculateExecutionTimeForAncestor(TaskNote taskNote, long newCollected, long oldCollected)
        {
            using (TaskContext db = new TaskContext(options))
            {
                taskNote.CalculatedExecutionTime += newCollected - oldCollected;
                var old = db.TaskNotes.Find(taskNote.Id).CalculatedExecutionTime;
                UpdateDb(taskNote);

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == taskNote.Id).SingleOrDefault()?.Ancestor;
                if (ancestor == null) return;

                CalculateExecutionTimeForAncestor(ancestor, taskNote.CalculatedExecutionTime, old);
            }
        }

        private void IsSubtasksCompletable(TaskNote task)
        {
            List<TaskNote> subtasks = GetDescendants(task.Id);
            foreach (TaskNote subtask in subtasks)
                IsSubtasksCompletable(subtask);
            ValidateStatusChange(task.Status, Statuses.Done);
        }
        

        private void SetSubtaskDoneStatus(TaskNote task)
        {
            if (task.Status == Statuses.Done) return;

            List<TaskNote> subtasks = GetDescendants(task.Id);
            foreach (TaskNote subtask in subtasks)
            {
                SetSubtaskDoneStatus(subtask);
            }

            task.Status = Statuses.Done;
            UpdateLogic(task);
        }

        

        private enum Commands { Start = 0, Pause, Resume, Finish, Clear, Nothing }

        private Commands ValidateStatusChange(Statuses curValue, Statuses newValue)
        {
            if (curValue == newValue) return Commands.Nothing;

            if (curValue == Statuses.InProgress && newValue == Statuses.Done) return Commands.Finish;
            if (curValue == Statuses.InProgress && newValue == Statuses.Pause) return Commands.Pause;
            if (curValue == Statuses.Pause && newValue == Statuses.InProgress) return Commands.Resume;
            if (curValue == Statuses.ToDo && newValue == Statuses.InProgress) return Commands.Start;
            if (curValue == Statuses.Done && newValue == Statuses.ToDo) return Commands.Clear;
            if (curValue == Statuses.Done && newValue == Statuses.ToDo) return Commands.Clear;
            if (curValue == Statuses.InProgress && newValue == Statuses.ToDo) return Commands.Clear;
            if (curValue == Statuses.Pause && newValue == Statuses.ToDo) return Commands.Clear;

            throw new StatusException("Cannot change from " + curValue.ToString() + " to " + newValue);
        }

        private void ActionOnStatusChange(TaskNote task, Commands command)
        {
            if (command == Commands.Nothing) return;

            else if (command == Commands.Start) task.StartExecutionDate = DateTime.Now;
            else if (command == Commands.Pause) task.ExecutionTime += (DateTime.Now - task.StartExecutionDate).Ticks;
            else if (command == Commands.Resume) task.StartExecutionDate = DateTime.Now;

            else if (command == Commands.Finish)
            {
                task.FinishDate = DateTime.Now;
                task.ExecutionTime += (DateTime.Now - task.StartExecutionDate).Ticks;
            }
            else if (command == Commands.Clear)
            {
                task.CreationDate = DateTime.Now;
                task.FinishDate = default(DateTime);
                task.StartExecutionDate = default(DateTime);
                task.ExecutionTime = default(TimeSpan).Ticks;
            }
        }
    }

    public class StatusException : InvalidOperationException
    {
        public StatusException(string message) : base(message) { }
    }
}