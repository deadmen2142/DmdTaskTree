using DmdTaskTree.DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DmdTaskTree.Tests
{
    public class TaskTreeManagerTest
    {
        DbContextOptions<TaskContext> options = new DbContextOptionsBuilder<TaskContext>()
            .UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=testdb1;Trusted_Connection=True;").Options;

        [Fact]
        public void Add_ThrowsNotFoundExceptionTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            Action act = () => manager.Add(tasks[0], tasks[1]);

            Assert.Throws<NotFoundException>(act);
        }

        [Fact]
        public void Add_ThrowsAddingExceptionTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote task = new TaskNote { Name = "1" };
            TaskNote task2 = new TaskNote { Name = "2" };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(task);
            Action act1 = () => manager.Add(task);
            manager.Add(task2);
            Action act2 = () => manager.Add(task2);

            Assert.Throws<AddingException>(act1);
            Assert.Throws<AddingException>(act2);
        }

        [Fact]
        public void Remove_ThorwsNotFoundExceptionTest()
        {
            TestHelper.ClearDatabase(options);

            int id = -1;
            TaskTreeManager manager = new TaskTreeManager(options);

            Action act = () => manager.Remove(id);

            Assert.Throws<NotFoundException>(act);

        }

        [Fact]
        public void Remove_ThrowsNonTerminalExceptionTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            Action act = () => manager.Remove(tasks[0].Id);

            Assert.Throws<NonTerminalException>(act);
        }

        [Fact]
        public void Remove_RemoveTerminalTaskTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Remove(tasks[1].Id);

            Assert.Null(manager.Find(tasks[1].Id));
            Assert.Empty(manager.GetDescendats(tasks[0].Id));
        }

        [Fact]
        public void Update_UpdateTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote task = new TaskNote { Name = "1t" };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(task);
            task.Name = "1t_updated";
            manager.Update(task);

            Assert.Equal(task.Name, manager.Find(task.Id).Name);
        }

        [Fact]
        public void Update_AddNonExistentTaskOnUpdateTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote task = new TaskNote { Name = "1t" };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Update(task);

            Assert.NotNull(manager.Find(task.Id));
        }

        [Fact]
        public void IsTerminal_Test()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);

            Assert.True(manager.IsTerminal(tasks[1].Id));
            Assert.False(manager.IsTerminal(tasks[0].Id));
        }

        [Fact]
        public void GetDescendats_Test()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" }, new TaskNote { Name = "3" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[0]);
            List<TaskNote> descendats = manager.GetDescendats(tasks[0].Id);

            Assert.Equal(2, descendats.Count);
            Assert.Single(descendats.Where(d => d.Id == tasks[1].Id));
            Assert.Single(descendats.Where(d => d.Id == tasks[2].Id));
            Assert.Empty(manager.GetDescendats(tasks[1].Id));
        }

        [Fact]
        public void HasDescendats_Test()
        {
            TestHelper.ClearDatabase(options);
            TaskNote[] tasks = { new TaskNote { Name = "1t" }, new TaskNote { Name = "2t" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);

            Assert.True(manager.HasDescendats(tasks[0].Id));
            Assert.False(manager.HasDescendats(tasks[1].Id));
        }

        [Fact]
        public void GetAncestor_Test()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" }, new TaskNote { Name = "3" } };
            TaskTreeManager manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[0]);

            Assert.Null(manager.GetAncestor(tasks[0].Id));
            Assert.Equal(tasks[0].Id, manager.GetAncestor(tasks[1].Id).Id);
            Assert.Equal(tasks[0].Id, manager.GetAncestor(tasks[2].Id).Id);
        }

        [Fact]
        public void Add_HierarchyTest()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = { new TaskNote { Name = "1" }, new TaskNote { Name = "2" }, new TaskNote { Name = "3" } };
            var manager = new TaskTreeManager(options);

            manager.Add(tasks[0]);
            manager.Add(tasks[1], tasks[0]);
            manager.Add(tasks[2], tasks[0]);

            var descendats = manager.GetDescendats(tasks[0].Id);
            Action act = () => manager.Add(null);

            Assert.Equal(tasks[0].Id, manager.GetAncestor(tasks[1].Id).Id);
            Assert.Equal(tasks[0].Id, manager.GetAncestor(tasks[2].Id).Id);
            Assert.Single(descendats.Where(t => t.Id == tasks[1].Id));
            Assert.Single(descendats.Where(t => t.Id == tasks[2].Id));
            Assert.Throws<NullReferenceException>(act);
        }

        [Fact]
        public void GetRoots()
        {
            TestHelper.ClearDatabase(options);

            TaskNote[] tasks = new TaskNote[4];
            TaskTreeManager manager = new TaskTreeManager(options);
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = new TaskNote { Name = (i + 1).ToString() + "t" };

            manager.Add(tasks[0]);
            manager.Add(tasks[1]);
            manager.Add(tasks[2]);
            manager.Add(tasks[3], tasks[0]);
            List<TaskNote> roots = manager.GetRoots();

            Assert.Equal(3, roots.Count);
            foreach (TaskNote root in roots)
            {
                Assert.NotNull(root);
            }
        }
    }
}