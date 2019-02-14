using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using DmdTaskTree.Models;

namespace DmdTaskTree.Tests
{
    public class TaskModelTests
    {
        [Fact]
        public void TaskModel_ThrowsExceptionOnWrongDateTest()
        {
            var tommorow = DateTime.Now.AddDays(1);
            var model = new TaskModel { PlanedFinishDate = tommorow };

            // Valid
            var time = model.GetPlanedExecutionTime();

            var yesterday = DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0));
            model.PlanedFinishDate = yesterday;

            Assert.Throws<WrongDateException>(() => model.GetPlanedExecutionTime());
        }
    }
}
