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
                    CalculatePlanedExecutionTimeForAncestor(ancestor, task.CalculatedPlanedExecutionTime);
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
                    CalculatePlanedExecutionTimeForAncestor(ancestor, default(TimeSpan).Ticks);
                    CalculateExecutionTimeForAncestor(ancestor, default(TimeSpan).Ticks);
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
                TaskNote curTask = db.TaskNotes.Where(t => t.Id == newTask.Id).SingleOrDefault();
                if (curTask == null) throw new NotFoundException("Task is not found in database", newTask.Id);

                Commands command = ValidateStatusChange(curTask.Status, newTask.Status);
                ActionOnStatusChange(newTask, command);

                // Update calculated field
                newTask.CalculatedPlanedExecutionTime = curTask.CalculatedPlanedExecutionTime - curTask.PlanedExecutionTime + newTask.PlanedExecutionTime;
                newTask.CalculatedExecutionTime = curTask.CalculatedExecutionTime - curTask.ExecutionTime + newTask.ExecutionTime;

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == newTask.Id).SingleOrDefault()?.Ancestor;
                if (ancestor != null)
                {
                    CalculatePlanedExecutionTimeForAncestor(ancestor, newTask.CalculatedPlanedExecutionTime);

                    if (command == Commands.Pause || command == Commands.Clear || command == Commands.Finish)
                        CalculateExecutionTimeForAncestor(ancestor, newTask.CalculatedExecutionTime);

                }

                // Status logic
                if (newTask.Status == Statuses.Done)
                    DoneStatusUpdateLogic(newTask);
                else
                    _Update(newTask);
            }
        }

        private void CalculatePlanedExecutionTimeForAncestor(TaskNote taskNote, long collected)
        {
            using (TaskContext db = new TaskContext(options))
            {
                taskNote.CalculatedPlanedExecutionTime = collected + taskNote.PlanedExecutionTime;
                _Update(taskNote);

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == taskNote.Id).SingleOrDefault()?.Ancestor;
                if (ancestor == null) return;

                CalculatePlanedExecutionTimeForAncestor(ancestor, taskNote.CalculatedPlanedExecutionTime);
            }
        }

        private void CalculateExecutionTimeForAncestor(TaskNote taskNote, long collected)
        {
            using (TaskContext db = new TaskContext(options))
            {
                taskNote.CalculatedExecutionTime = collected + taskNote.ExecutionTime;
                _Update(taskNote);

                TaskNote ancestor = db.TaskTreeNodes.Include(t => t.Ancestor).Where(t => t.DescendantId == taskNote.Id).SingleOrDefault()?.Ancestor;
                if (ancestor == null) return;

                CalculateExecutionTimeForAncestor(ancestor, taskNote.CalculatedExecutionTime);
            }
        }

        private void DoneStatusUpdateLogic(TaskNote newTask)
        {
            List<TaskNote> subtasks = GetDescendants(newTask.Id);
            foreach (TaskNote subtask in subtasks)
            {
                UpdateSubtaskStatus(subtask, Statuses.Done);
            }

            _Update(newTask);
        }

        private void UpdateSubtaskStatus(TaskNote task, Statuses newStatus)
        {
            if (task.Status == newStatus) return;

            var command = ValidateStatusChange(task.Status, newStatus);
            ActionOnStatusChange(task, command);

            List<TaskNote> subtasks = GetDescendants(task.Id);
            foreach (TaskNote subtask in subtasks)
            {
                UpdateSubtaskStatus(subtask, newStatus);
            }

            task.Status = newStatus;
            _Update(task);
        }

        private void _Update(TaskNote task)
        {
            using (TaskContext db = new TaskContext(options))
            {
                TaskNote curTask = db.TaskNotes.FirstOrDefault(t => t.Id == task.Id);
                db.Entry(curTask).CurrentValues.SetValues(task);
                db.SaveChanges();
            }
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