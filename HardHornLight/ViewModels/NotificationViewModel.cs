using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;

namespace HardHorn.ViewModels
{
    public class NotificationViewModel : PropertyChangedBase, INotification
    {
        public NotificationType Type { get; private set; }
        public Severity Severity { get; private set; }
        public Test Test { get; set; }
        public Column Column { get; set; }
        public Table Table { get; set; }
        public string Header { get; set; }
        public string Message { get; set; }
        public int? Count
        {
            get { return count; }
            set
            {
                count = value;
                if (value != null && NotifyTimer.Enabled == false)
                {
                    NotifyTimer.Start();
                }
            }
        }

        Timer NotifyTimer;
        int? count;

        public NotificationViewModel(INotification notification)
        {
            NotifyTimer = new Timer(250.0d);
            NotifyTimer.Elapsed += (o, ae) =>
            {
                NotifyOfPropertyChange("Count");
            };
            Type = notification.Type;
            Severity = notification.Severity;
            Table = notification.Table;
            Column = notification.Column;
            Message = notification.Message;
            Count = notification.Count;
        }

        public NotificationViewModel(NotificationType type, Severity severity)
        {
            Type = type;
            Severity = severity;
            NotifyTimer = new Timer(250.0d);
            NotifyTimer.Elapsed += (o, ae) =>
            {
                NotifyOfPropertyChange("Count");
            };
            Count = 1;
        }
    }
}
