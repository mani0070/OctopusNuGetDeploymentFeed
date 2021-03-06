﻿using System;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using Microsoft.Win32.TaskScheduler;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed
{
    public class Watchdog
    {
        public const string ArgName = "watchdog-check";
        public const string TaskName = "Octopus Deploy NuGet Feed Watchdog";
        public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
        private readonly ILogger _logger;

        public Watchdog(ILogger logger)
        {
            _logger = logger;
        }

        public void CreateTask()
        {
            _logger.Info($"Creating Scheduled Task: {TaskName}");
            var task = TaskService.Instance.NewTask();
            task.Principal.UserId = "SYSTEM";
            task.Principal.LogonType = TaskLogonType.ServiceAccount;

            task.Triggers.Add(new BootTrigger
            {
                Repetition =
                {
                    Duration = TimeSpan.Zero,
                    Interval = CheckInterval
                }
            });

            task.Triggers.Add(new RegistrationTrigger
            {
                Delay = TimeSpan.FromMinutes(5),
                Repetition =
                {
                    Duration = TimeSpan.Zero,
                    Interval = CheckInterval
                }
            });

            task.Actions.Add(Assembly.GetExecutingAssembly().Location, ArgName);

            TaskService.Instance.RootFolder.RegisterTaskDefinition(TaskName, task);
            _logger.Info("Scheduled Task has been created.");
        }

        public void DeleteTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask(TaskName, false);
                _logger.Info($"Scheduled Task Deleted: {TaskName}");
            }
        }

        public void Check()
        {
            try
            {
                var service = ServiceController.GetServices().SingleOrDefault(controller => string.Equals(controller.ServiceName, nameof(OctopusDeployNuGetFeed)));
                if (service == null)
                {
                    _logger.Error("Watchdog.Check: Service Does Not Exist!");
                    return;
                }

                _logger.Info($"Watchdog.Check: Service is {service.Status}");
                if (service.Status == ServiceControllerStatus.Running || service.Status == ServiceControllerStatus.StartPending)
                    return;

                _logger.Warning("Service is not running! Starting...");
                service.Start();
                service.Refresh();
                _logger.Info($"Service is {service.Status}, it will be checked again in {CheckInterval:g}");
            }
            catch (Exception e)
            {
                _logger.Exception(e);
            }
        }
    }
}