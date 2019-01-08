using Caliburn.Micro;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace HardHorn.ViewModels
{
    public class TaskViewModel : PropertyChangedBase
    {
        bool running = false;
        bool done = false;
        bool errors = false;
        System.Action action;

        public string Name { get; private set; }
        public bool Running { get { return running; } private set { running = value; NotifyOfPropertyChange("Running"); } }
        public bool Done { get { return done; } private set { done = value; NotifyOfPropertyChange("Done"); } }
        public bool Errors { get { return errors; } private set { errors = value; NotifyOfPropertyChange("Errors"); } }
        public Exception Exception { get; private set; }

        public TaskViewModel(string name, System.Action action)
        {
            Name = name;

            Running = false;
            Done = false;
            Errors = false;
            Exception = null;

            this.action = action;
        }

        public async Task Run()
        {
            try
            {
                Running = true;
                await Task.Run(action);
                Done = true;
            }
            catch (Exception ex)
            {
                Exception = ex;
                NotifyOfPropertyChange("Exception");
            }   
            finally
            {
                Running = false;
            }
        }
    }
}
