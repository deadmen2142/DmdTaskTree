using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DmdTaskTree.Models;
using Microsoft.EntityFrameworkCore;
using DmdTaskTree.DataAccessLayer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;

namespace DmdTaskTree.Controllers
{
    public class HomeController : Controller
    {
        private TaskManager manager;

        public HomeController()
        {
            string connection = "Server=(localdb)\\mssqllocaldb;Database=tasktreedb;Trusted_Connection=True;";
            manager = new TaskManager(new DbContextOptionsBuilder<TaskContext>().UseSqlServer(connection).Options);
        }


        public IActionResult Index()
        {
            TaskListModelPreview model = new TaskListModelPreview
            {
                Tasks = manager.GetRoots().Select(t => new TaskModelPreview(t)).ToArray()
            };

            TaskModelPreview[] roots = model.Tasks;
            for (int i = 0; i < roots.Length; i++)
                LoadDescendants(ref roots[i]);

            return View(model);
        }

        private void LoadDescendants(ref TaskModelPreview task)
        {
            if (!manager.HasDescendants(task.Id)) return;

            task.Descendants = manager.GetDescendants(task.Id).Select(t => new TaskModelPreview(t)).ToArray();
            for (int i = 0; i < task.Descendants.Length; i++)
                LoadDescendants(ref task.Descendants[i]);
        }

        [HttpGet]
        public IActionResult Create(int id)
        {
            ViewBag.AncestorId = id;
            return View();
        }

        [HttpPost]
        public IActionResult Create(TaskModel model)
        {
            try
            {
                TaskNote task = new TaskNote
                {
                    Name = model.Name,
                    Description = model.Description,
                    Performers = model.Performers,
                    PlanedExecutionTime = model.GetPlanedExecutionTime()
                };

                if (model.AncestorId != default(int))
                    manager.Add(task, model.AncestorId);
                else
                    manager.Add(task);
            }
            catch (InvalidOperationException ex)
            {
                return View("Error", ex.Message);
            }

            return Redirect("~/Home/Index");
        }

        [HttpGet]
        public IActionResult Remove(int id)
        {
            try
            {
                manager.Remove(id);
            }
            catch (InvalidOperationException ex)
            {
                return View("Error", ex.Message);
            }

            return Redirect("~/Home/Index");
        }

        [HttpGet]
        public IActionResult GetTask(int id)
        {
            TaskModelOutput output = new TaskModelOutput();
            try
            {
                var task = manager.Find(id);

                output.Id = task.Id;
                output.Name = task.Name;
                output.Description = task.Description;
                output.Performers = task.Performers;
                output.CreationDate = TimeToString(task.CreationDate);
                output.FinishDate = TimeToString(task.FinishDate);
                output.Status = task.Status;
                
                output.CalculatedPlanedExecutionTime = TimeToString(TimeSpan.FromTicks(task.CalculatedPlanedExecutionTime));
                output.ExecutionTime = TimeToString(TimeSpan.FromTicks(task.ExecutionTime));
                output.SubtaskExecutionTime = TimeToString(TimeSpan.FromTicks(task.CalculatedExecutionTime - task.ExecutionTime));
                
            }
            catch (InvalidOperationException ex)
            {
                return View("Error", ex.Message);
            }

            return PartialView("Description", output);
        }

        private string TimeToString(TimeSpan time)
        {
            return time == default(TimeSpan) ? "" : time.ToString(@"dd\.hh\:mm\:ss") + " days";
        }

        private string TimeToString(DateTime time)
        {
            return time == default(DateTime) ? "" : time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        [HttpGet]
        public IActionResult Update(int id)
        {
            TaskNote task;
            try
            {
                task = manager.Find(id);
            }
            catch (InvalidOperationException ex)
            {
                return View("Error", ex.Message);
            }

            return View(task);
        }

        [HttpPost]
        public IActionResult Update(TaskModel task)
        {
            TaskNote taskNote;
            try
            {
                taskNote = manager.Find(task.Id);

                taskNote.Name = task.Name;
                taskNote.Description = task.Description;
                taskNote.Performers = task.Performers;
                taskNote.PlanedExecutionTime = task.GetPlanedExecutionTime();
                taskNote.Status = task.GetStatus();

                manager.Update(taskNote);
            }
            catch (InvalidOperationException ex)
            {
                return View("Error", ex.Message);
            }

            return Redirect("~/Home/Index");
        }
    }
}
