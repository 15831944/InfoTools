using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavisWorksInfoTools
{
    //НЕ РАБОТАЕТ!!!
    //[Plugin("EventWatcher", DEVELOPER_ID,
    //  DisplayName = "Event Watcher", SupportsIsSelfEnabled = false)]

    //class EventController : EventWatcherPlugin
    //{
    //    public override void OnLoaded()
    //    {
    //        Application.ActiveDocumentChanged += ActiveDocumentChanged_EventHandler;
    //        Application.DocumentAdded += DocumentAdded_EventHandler;
    //        Application.DocumentRemoved += DocumentRemoved_EventHandler;
    //        Application.MainDocumentChanged += MainDocumentChanged_EventHandler;
    //    }

    //    public override void OnUnloading()
    //    {
    //        Application.ActiveDocumentChanged -= ActiveDocumentChanged_EventHandler;
    //        Application.DocumentAdded -= DocumentAdded_EventHandler;
    //        Application.DocumentRemoved -= DocumentRemoved_EventHandler;
    //        Application.MainDocumentChanged -= MainDocumentChanged_EventHandler;
    //    }


    //    public void ActiveDocumentChanged_EventHandler<TEventArgs>(object sender, TEventArgs e)
    //    {
    //        //Application.ActiveDocument.TransactionEnded +=;
    //    }
    //    public void DocumentAdded_EventHandler<TEventArgs>(object sender, TEventArgs e)
    //    {
    //    }
    //    public void DocumentRemoved_EventHandler<TEventArgs>(object sender, TEventArgs e)
    //    {
    //    }
    //    public void MainDocumentChanged_EventHandler<TEventArgs>(object sender, TEventArgs e)
    //    {
    //    }

    //}
}
