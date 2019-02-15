using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Xunit;
using System.Threading;
using DmdTaskTree.DataAccessLayer;
using DmdTaskTree.Models;

namespace DmdTaskTree.Tests
{
    public class TaskManagerTests
    {
        DbContextOptions<TaskContext> options = new DbContextOptionsBuilder<TaskContext>()
            .UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=testdb2;Trusted_Connection=True;").Options;
        // comment
        [Fact]
        public void Update_ThrowsNotFoundExceptionTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            TaskNote task = new TaskNote { Name = "1t" };

            Action act = () => manager.Update(task);

            Assert.Throws<NotFoundException>(act);
        }

        [Fact]
        public void Update_ValidateStatusChange()
        {
            TestHelper.ClearDatabase(options);

            TaskManager manager = new TaskManager(options);
            TaskNote task = new TaskNote { Name = "1t" };
            Statuses[] validChanges = {
                Statuses.InProgress,
                Statuses.ToDo,
                Statuses.InProgress,
                Statuses.Pause,
                Statuses.InProgress,
                Statuses.Done,
                Statuses.ToDo };

            Assert.Equal(Statuses.ToDo, task.Status);
            manager.Add(task);

            // Must be valid and dont throw exceptions
            for (int i = 0; i < validChanges.Length; i++)
            {
                task.Status = validChanges[i];
                manager.Update(task);
            }

            task.Status = Statuses.Pause;
            Assert.Throws<StatusException>(() => manager.Update(task));

            task.Status = Statuses.Done;
            Assert.Throws<StatusException>(() => manager.Update(task));

            task.Status = Statuses.InProgress;
            manager.Update(task);
            task.Status = Statuses.Pause;
            manager.Update(task);
            task.Status = Statuses.ToDo;
            manager.Update(task);

            task.Status = Statuses.Done;
            Assert.Throws<StatusException>(() => manager.Update(task));

            task.Status = Statuses.InProgress;
            manager.Update(task);
            task.Status = Statuses.Done;
            manager.Update(task);

            task.Status = Statuses.Pause;
            Assert.Throws<StatusException>(() => manager.Update(task));
        }

        [Fact]
        public void Update_AncestorMustBeDoneOnlyAfterDescendantsWillBeDone()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            TaskNote[] tasks = new TaskNote[5];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = (i + 1).ToString() + "t" };

            /*
             * task 0
             *   task 1
             *      task 2
             *          task 3
             * task 4
             */
            manager.Add(tasks[0]);
            manager.Add(tasks[4]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);

            tasks[0].Status = Statuses.InProgress;
            manager.Update(tasks[0]);

            tasks[1].Status = Statuses.InProgress;
            manager.Update(tasks[1]);

            // task[2].Status still ToDo

            tasks[3].Status = Statuses.InProgress;
            manager.Update(tasks[3]);
            tasks[3].Status = Statuses.Done;
            manager.Update(tasks[3]);

            tasks[0].Status = Statuses.Done;
            Assert.Throws<StatusException>(() => manager.Update(tasks[0]));

            // Local instance's status is Done
            Assert.True(tasks[0].Status == Statuses.Done);

            // But if will update instance from db, status will be InProgress
            tasks[0] = manager.Find(tasks[0].Id);
            Assert.True(tasks[0].Status == Statuses.InProgress);

            // Descendants statuses haven't changed
            Assert.True(tasks[1].Status == Statuses.InProgress);
            Assert.True(tasks[2].Status == Statuses.ToDo);
            Assert.True(tasks[3].Status == Statuses.Done);

            // 
            tasks[2].Status = Statuses.InProgress;
            manager.Update(tasks[2]);

            tasks[0].Status = Statuses.Done;
            manager.Update(tasks[0]);

            Assert.Equal(Statuses.InProgress, tasks[2].Status);

            for (int i = 0; i < 4; i++)
            {
                tasks[i] = manager.Find(tasks[i].Id);
                Assert.Equal(Statuses.Done, tasks[i].Status);
                Assert.Equal(DateTime.Now.Date, tasks[i].FinishDate.Date);
            }
            Assert.Equal(Statuses.ToDo, tasks[4].Status);
        }

        [Fact]
        public void OnAdd_Test()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            var task = new TaskNote { Name = "1t", PlanedExecutionTime = new TimeSpan(0, 0, 10).Ticks };

            Assert.True(task.CreationDate == default(DateTime));
            Assert.True(task.CalculatedPlanedExecutionTime == default(TimeSpan).Ticks);

            manager.Add(task);
            task = manager.Find(task.Id);

            Assert.Equal(DateTime.Now.Date, task.CreationDate.Date);
            Assert.Equal(task.PlanedExecutionTime, task.CalculatedPlanedExecutionTime);
        }

        [Fact]
        public void Add_CollectSubtasksPlanedExecutionTimeTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            int count = 5;
            int sec = 10;
            TaskNote[] tasks = new TaskNote[count];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = i.ToString(), PlanedExecutionTime = new TimeSpan(0, 0, sec).Ticks };

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);
            manager.Add(tasks[4], tasks[3]);

            // Refresh and assert
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = manager.Find(tasks[i].Id);
                Assert.Equal(GetSec((tasks.Length - i) * sec), (int)tasks[i].CalculatedPlanedExecutionTime);
            }
        }

        [Fact]
        public void Add_CanAddTaskWithOnlyTodoStatusTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            // Valid
            manager.Add(new TaskNote { Status = Statuses.ToDo }); // ToDo is default status;

            Assert.Throws<StatusException>(() => manager.Add(new TaskNote { Status = Statuses.InProgress }));
            Assert.Throws<StatusException>(() => manager.Add(new TaskNote { Status = Statuses.Pause }));
            Assert.Throws<StatusException>(() => manager.Add(new TaskNote { Status = Statuses.Done }));
        }

        [Fact]
        public void Add_CanNotAddSubtaskToCompleteTaskTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            var ancestor = new TaskNote();
            var decendat = new TaskNote();

            manager.Add(ancestor);
            ancestor.Status = Statuses.InProgress;
            manager.Update(ancestor);
            ancestor.Status = Statuses.Done;
            manager.Update(ancestor);
            
            Assert.Throws<StatusException>(() => manager.Add(decendat, ancestor));
        }

        [Fact]
        public void Remove_Test()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            int count = 5;
            TaskNote[] tasks = new TaskNote[count];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = i.ToString() };

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);
            manager.Add(tasks[4], tasks[3]);

            Assert.Throws<NonTerminalException>(() => manager.Remove(tasks[0].Id));

            for (int i = count - 1; i > 0; i--)
            {
                manager.Remove(tasks[i].Id);

                Assert.Null(manager.Find(tasks[i].Id));
                Assert.Empty(manager.GetDescendants(tasks[i - 1].Id));
            }

            manager.Remove(tasks[0].Id);
            Assert.Null(manager.Find(tasks[0].Id));
        }

        [Fact]
        public void Remove_RecomputePlanedExecutionTimeForEachTaskInHierarchyTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            int count = 5;
            int sec = 10;
            TaskNote[] tasks = new TaskNote[count];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = i.ToString(), PlanedExecutionTime = new TimeSpan(0, 0, sec).Ticks };

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);
            manager.Add(tasks[4], tasks[3]);

            for (int i = count - 1; i > 0; i--)
            {
                manager.Remove(tasks[i].Id);
                tasks[i] = null;

                tasks[0] = manager.Find(tasks[0].Id);
                Assert.Equal(GetSec(i * sec), (int)tasks[0].CalculatedPlanedExecutionTime);
            }
        }

        [Fact]
        public void Update_RecomputePlanedExecutionTimeForEachTaskInHierarchyTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            int count = 5;
            int sec = 10;
            TaskNote[] tasks = new TaskNote[count];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = i.ToString(), PlanedExecutionTime = new TimeSpan(0, 0, sec).Ticks };

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);
            manager.Add(tasks[4], tasks[3]); // tasks[0].Calculated... == 50

            tasks[4].PlanedExecutionTime = default(TimeSpan).Ticks;
            manager.Update(tasks[4]); // tasks[0].Calculated... == 40 case tasks[4].Planed.. == 0 sec

            

            tasks[0] = manager.Find(tasks[0].Id);
            Assert.Equal(GetSec(4 * sec), (int)tasks[0].CalculatedPlanedExecutionTime);

            tasks[3].PlanedExecutionTime = new TimeSpan(0, 0, 2 * sec).Ticks; // 40 - 10 + 20
            manager.Update(tasks[3]); // tasks[0].Calculated... == 50

            tasks[0] = manager.Find(tasks[0].Id);
            Assert.Equal(GetSec(count * sec), (int)tasks[0].CalculatedPlanedExecutionTime);

            tasks[0].PlanedExecutionTime = new TimeSpan(0, 0, 4 * sec).Ticks; // 50 - 10 + 40
            manager.Update(tasks[0]);

            tasks[0] = manager.Find(tasks[0].Id);
            Assert.Equal(GetSec(8 * sec), (int)tasks[0].CalculatedPlanedExecutionTime); // // tasks[0].Calculated... == 80 sec
        }

        long GetSec(int s) => new TimeSpan(0, 0, s).Ticks;

        [Fact]
        public void Update_RecordExecutionTimeTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            TaskNote task = new TaskNote();
            manager.Add(task);

            // Record
            task.Status = Statuses.InProgress;
            manager.Update(task);

            Thread.Sleep(1000);

            task.Status = Statuses.Pause;
            manager.Update(task);
            TimeSpan executionTime = TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime);
            Assert.True(new TimeSpan(0, 0, 1) <= executionTime);

            // Doesnt record after pause
            Thread.Sleep(1000);

            Assert.Equal(executionTime, TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime));


            // Resume recording
            task.Status = Statuses.InProgress;
            manager.Update(task);

            Thread.Sleep(1000);

            task.Status = Statuses.Pause;
            manager.Update(task);

            executionTime = TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime);
            Assert.True(new TimeSpan(0, 0, 2) <= executionTime);


            // Finish recording (same with pause)
            task.Status = Statuses.InProgress;
            manager.Update(task);

            Thread.Sleep(1000);

            task.Status = Statuses.Done;
            manager.Update(task);
            executionTime = TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime);
            Assert.True(new TimeSpan(0, 0, 3) <= executionTime);

            // Doesnt record after finish
            Thread.Sleep(1000);

            Assert.Equal(executionTime, TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime));


            // Change status from Done to ToDo to clear execution time 
            task.Status = Statuses.ToDo;
            manager.Update(task);

            executionTime = TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime);
            Assert.Equal(default(TimeSpan), executionTime);


            // Record after clear
            task.Status = Statuses.InProgress;
            manager.Update(task);

            Thread.Sleep(1000);

            task.Status = Statuses.Pause;
            manager.Update(task);
            executionTime = TimeSpan.FromTicks(manager.Find(task.Id).ExecutionTime);
            Assert.True(new TimeSpan(0, 0, 1) <= executionTime);
        }

        [Fact]
        public void Update_RecomputeExecutionTImeTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            TaskNote[] tasks = new TaskNote[4];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote();

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[0]);
            manager.Add(tasks[3], tasks[1]);

            TestHelper.SetStatus(manager, tasks, Statuses.InProgress);
            Thread.Sleep(1000);

            tasks[3].Status = Statuses.Done;
            manager.Update(tasks[3]);

            /*
             * task0 progress
             *   task1 progress
             *     task3 done
             *   task2 progress
             */

            tasks[0] = manager.Find(tasks[0].Id);
            tasks[0].Status = Statuses.Done;
            manager.Update(tasks[0]);

            TestHelper.Refresh(manager, tasks);

            Assert.Equal(tasks[3].CalculatedExecutionTime, tasks[3].ExecutionTime);
            Assert.Equal(tasks[2].CalculatedExecutionTime, tasks[2].ExecutionTime);

            Assert.Equal(tasks[1].CalculatedExecutionTime, tasks[1].ExecutionTime + tasks[3].CalculatedExecutionTime);
            Assert.Equal(tasks[0].CalculatedExecutionTime, tasks[1].CalculatedExecutionTime + tasks[2].CalculatedExecutionTime + tasks[0].ExecutionTime);
        }

        

        [Fact]
        public void Update_CollectSubtaskExecutionTimeTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            TaskNote[] tasks = new TaskNote[5];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote();

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);
            manager.Add(tasks[3], tasks[2]);
            manager.Add(tasks[4], tasks[3]);

            // Execution time of tasks[4] equal 1 sec
            tasks[4].Status = Statuses.InProgress;
            manager.Update(tasks[4]);

            Thread.Sleep(1000);

            tasks[4].Status = Statuses.Done;
            manager.Update(tasks[4]);

            // And main task (tasks[0]) must collect tasks[4] execution time in calculated field
            Assert.Equal(manager.Find(tasks[0].Id).CalculatedExecutionTime, manager.Find(tasks[4].Id).CalculatedExecutionTime);


            // Apply null execution time after clear
            tasks[4].Status = Statuses.ToDo;
            manager.Update(tasks[4]);

            long executionTime = manager.Find(tasks[4].Id).CalculatedExecutionTime;
            Assert.Equal(default(TimeSpan).Ticks, executionTime);
            Assert.Equal(manager.Find(tasks[0].Id).CalculatedExecutionTime, executionTime);

            // TURBO TEST
            executionTime = default(TimeSpan).Ticks;
            for (int i = tasks.Length - 1; i >= 0; i--)
            {
                tasks[i].Status = Statuses.InProgress;
                manager.Update(tasks[i]);

                Thread.Sleep(1000);

                tasks[i].Status = Statuses.Pause;
                manager.Update(tasks[i]);
                tasks[i] = manager.Find(tasks[i].Id);
            }
            var sec = new TimeSpan(0, 0, 1).Ticks;
            Assert.True(tasks[4].ExecutionTime >= sec && tasks[4].ExecutionTime < 2*sec);
            Assert.Equal(tasks[4].CalculatedExecutionTime, tasks[4].ExecutionTime);

            executionTime += tasks[4].ExecutionTime + tasks[3].ExecutionTime;
            Assert.Equal(tasks[3].CalculatedExecutionTime, executionTime);

            executionTime += tasks[2].ExecutionTime;
            Assert.Equal(tasks[2].CalculatedExecutionTime, executionTime);

            executionTime += tasks[1].ExecutionTime;
            Assert.Equal(tasks[1].CalculatedExecutionTime, executionTime);

            executionTime += tasks[0].ExecutionTime;
            Assert.Equal(tasks[0].CalculatedExecutionTime, executionTime);
        }

        [Fact]
        public void Remove_ApplyNullExecutionTimeTest()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);

            TaskNote[] tasks = new TaskNote[3];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote();

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[1]);

            for (int i = tasks.Length - 1; i >= 0; i--)
            {
                tasks[i].Status = Statuses.InProgress;
                manager.Update(tasks[i]);

                Thread.Sleep(1000);

                tasks[i].Status = Statuses.Pause;
                manager.Update(tasks[i]);
                tasks[i] = manager.Find(tasks[i].Id);
            }

            manager.Remove(tasks[2].Id);
            Assert.Equal(manager.Find(tasks[0].Id).CalculatedExecutionTime, tasks[0].CalculatedExecutionTime - tasks[2].ExecutionTime);

            tasks[0] = manager.Find(tasks[0].Id);
            manager.Remove(tasks[1].Id);
            Assert.Equal(manager.Find(tasks[0].Id).CalculatedExecutionTime, tasks[0].CalculatedExecutionTime - tasks[1].ExecutionTime);

            tasks[0] = manager.Find(tasks[0].Id);
            Assert.Equal(tasks[0].ExecutionTime, tasks[0].CalculatedExecutionTime);
        }

        [Fact]
        public void Update_OnClearAndOnFinishDateChanges()
        {
            TestHelper.ClearDatabase(options);
            TaskManager manager = new TaskManager(options);
            TaskNote task = new TaskNote();
            manager.Add(task);

            Assert.Equal(DateTime.Now.Date, task.CreationDate.Date);
            Assert.Equal(default(DateTime), task.FinishDate);

            task.Status = Statuses.InProgress;
            manager.Update(task);
            task.Status = Statuses.Done;
            manager.Update(task);
            task = manager.Find(task.Id);

            Assert.Equal(DateTime.Now.Date, task.FinishDate.Date);

            task.CreationDate = new DateTime(1, 1, 1);
            task.Status = Statuses.ToDo;
            manager.Update(task);
            task = manager.Find(task.Id);

            Assert.Equal(DateTime.Now.Date, task.CreationDate.Date);
            Assert.Equal(default(DateTime), task.FinishDate);
        }
    }
}