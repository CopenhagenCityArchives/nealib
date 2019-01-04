using Caliburn.Micro;
using System;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    public class TaskViewModel : PropertyChangedBase
    {
        public string Name { get; private set; }

        bool running = false;
        public bool Running { get { return running; } private set { running = value; NotifyOfPropertyChange("Running"); } }
        bool done = false;
        public bool Done { get { return done; } private set { done = value; NotifyOfPropertyChange("Done"); } }
        bool errors = false;
        public bool Errors { get { return errors; } private set { errors = value; NotifyOfPropertyChange("Errors"); } }
        public Exception Exception { get; private set; }

        System.Action action;

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
            }   
            finally
            {
                Running = false;
            }
        }
    }
}
