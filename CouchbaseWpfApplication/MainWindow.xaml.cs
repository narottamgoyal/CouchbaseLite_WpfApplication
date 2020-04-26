using Couchbase.Lite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CouchbaseWpfApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        View view;
        const string DB_NAME = "sync_gateway";
        const string TAG = "CouchbaseEvents";
        const string byDateViewName = "byDate";

        Database db;
        public MainWindow()
        {
            InitializeComponent();
            LoadStartUp();
        }

        private void LoadStartUp()
        {
            try
            {
                db = Manager.SharedInstance.GetDatabase(DB_NAME);
                view = CreateEventsByDateView();
                LoadSavedDocuments(view);
            }
            catch (Exception e)
            {
                ShowError("LoadStartUp: " + e.Message);
                return;
            }

        }

        void ShowError(string error)
        {
            errorBox.Items.Add(error);
        }

        void UpdateDocumnetList(string doc)
        {
            docIdList.Items.Insert(0, doc);
        }

        async void LoadSavedDocuments(View cbView)
        {
            var orderedQuery = cbView.CreateQuery();
            orderedQuery.Descending = true;
            //orderedQuery.Limit = 20;
            try
            {
                var results = await orderedQuery.RunAsync();
                results.ToList().ForEach(result =>
                {
                    var doc = result.Document;
                    UpdateDocumnetList(result.DocumentId);
                    RetrieveDocument(result.DocumentId);
                });
            }
            catch (Exception ex)
            {
                ShowError("LoadSavedDocuments: " + ex.Message);
            }
        }
      
        private void UpdateDocument(string id, string key, object value)
        {
            itemList.Items.Insert(0, new Doc()
            {
                ID = id,
                Key = key,
                Name = string.Empty + value
            });
        }

        void RetrieveDocument(string docID)
        {
            // retrieve the document from the database
            var retrievedDoc = db.GetDocument(docID);
            // display the retrieved document
            //doc.Properties.Select(x => String.Format("key={0}, value={1}", x.Key, x.Value))
            //    .ToList().ForEach(y => itemList.Items.Add(TAG + " " + y));
            retrievedDoc.Properties.Where(x => x.Key == "name").ToList().ForEach(y =>
            {
                //itemList.Items.Add("Key: " + y.Key + " value:" + y.Value);
                UpdateDocument(retrievedDoc.Id, y.Key, y.Value);
            });
        }

        public View CreateEventsByDateView()
        {
            var eventsByDateView = db.GetView("eventsByDate");
            try
            {
                //eventsByDateView.SetMap((doc, emit) => emit(doc["date"].ToString(), null), "1.0");
                eventsByDateView.SetMap((doc, emit) => emit(doc, null), "1.0");
            }
            catch (CouchbaseLiteException ex)
            {
                ShowError("CreateEventsByDateView: " + ex.Message);
            }
            return eventsByDateView;
        }

        Uri CreateSyncUri()
        {
            Uri syncUri = null;
            string scheme = "http";
            string host = txtIP.Text;
            int port = Convert.ToInt32(txtPort.Text);
            string dbName = "sync_gateway";
            try
            {
                var uriBuilder = new UriBuilder(scheme, host, port, dbName);
                syncUri = uriBuilder.Uri;
            }
            catch (UriFormatException e)
            {
                ShowError("CreateSyncUri: " + e.Message);
            }
            return syncUri;
        }
        void StartReplications()
        {
            try
            {
                Replication pull = db.CreatePullReplication(CreateSyncUri());
                Replication push = db.CreatePushReplication(CreateSyncUri());
                pull.Continuous = true;
                push.Continuous = true;
                pull.Start();
                push.Start();
            }
            catch (Exception ex)
            {
                ShowError("StartReplications: " + ex.Message);
            }

        }

        private void listbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var x = sender as ListBox;
            if (x.SelectedItem != null)
                ShowDocumentInformation((x.SelectedItem as Doc).ID);
        }

        private void ShowDocumentInformation(string docId)
        {

            if (string.IsNullOrWhiteSpace(docId)) return;

            string result = string.Empty;
            var doc = db.GetExistingDocument(docId);
            try
            {
                doc.Properties.ToList().ForEach(y =>
                {
                    result += "Key: " + y.Key + "\tValue: " + y.Value + "\n";
                    if (y.Key == "name")
                        txtUpdate.Text = string.Empty + y.Value;
                });



                var savedReHv = doc.CurrentRevision.RevisionHistory;
                var savedRev = doc.CurrentRevision;
                var attachment = savedRev.GetAttachment("binaryData");
                if (attachment != null)
                {
                    using (var sr = new StreamReader(attachment.ContentStream))
                    {
                        var data = sr.ReadToEnd();
                        result += ("Attachment:\n" + data);
                    }
                }
            }
            catch (Exception ex)
            {
                var savedReHv = doc.CurrentRevision.RevisionHistory.ToList();
                var savedRev = savedReHv[savedReHv.Count - 2];
                var attachment = savedRev.GetAttachment("binaryData");
                if (attachment != null)
                {
                    using (var sr = new StreamReader(attachment.ContentStream))
                    {
                        var data = sr.ReadToEnd();
                        result += ("Attachment:\n" + data);
                    }
                }
                //ShowError(ex.Message);
            }
            docInfo.Text = result;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewItem.Text)) return;
            var doc = db.CreateDocument();
            string docId = doc.Id;
            var props = new Dictionary<string, object> {
                                                        { "name", txtNewItem.Text },
                                                        { "created_at", DateTime.Now.ToString() }
                                                    };
            try
            {
                doc.PutProperties(props);
                UpdateDocument(doc.Id, "name", txtNewItem.Text);
                UpdateDocumnetList(docId);
                ShowDocumentInformation(docId);
                txtUpdate.Text = txtNewItem.Text;
            }
            catch (Exception ex)
            {
                ShowError("Add_Click : "+ ex.Message);
            }
            finally
            {
                txtNewItem.Text = string.Empty;
            }
        }

        void AddAttachment(string docID)
        {
            var doc = db.GetDocument(docID);
            try
            {
                var revision = doc.CurrentRevision.CreateRevision();
                var data = Encoding.ASCII.GetBytes(txtAttach.Text);
                revision.SetAttachment("binaryData", "application/octet-stream", data);
                // Save the document and attachment to the local db
                revision.Save();
            }
            catch (Exception ex)
            {
                ShowError("AddAttachment: "+ ex.Message);
            }
        }

        private void docIdList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var x = sender as ListBox;
            if(x.SelectedItem != null)
            ShowDocumentInformation(x.SelectedItem as string);
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var docID = "";
            if ((itemList.SelectedItem as Doc) != null)
                docID = (itemList.SelectedItem as Doc).ID;
            else docID = docIdList.SelectedItem as string ?? "";
            if (docID == "")
            {
                ShowError("document is not selected");
                return;
            }

            var doc = db.GetDocument(docID);
            try
            {
                // Update the document with more data
                var updatedProps = new Dictionary<string, object>(doc.Properties);
                updatedProps["name"] = txtUpdate.Text;
                // Save to the Couchbase local Couchbase Lite DB
                doc.PutProperties(updatedProps);
                // display the updated document
                (itemList.SelectedItem as Doc).Name = txtUpdate.Text;
                //getDocInfo(doc.Id);
            }
            catch (CouchbaseLiteException ex)
            {
                ShowError("UpdateButton_Click : " + ex.Message);
            }
            finally
            {
                txtUpdate.Text = string.Empty;
            }
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var docID = "";
            if ((itemList.SelectedItem as Doc) != null)
                docID = (itemList.SelectedItem as Doc).ID;
            else docID = docIdList.SelectedItem as string ?? "";
            if (docID == "")
            {
                ShowError("document is not selected");
                return;
            }
            AddAttachment(docID);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var docID = "";
                if ((itemList.SelectedItem as Doc) != null)
                    docID = (itemList.SelectedItem as Doc).ID;
                else docID = docIdList.SelectedItem as string ?? "";
                if (docID == "")
                {
                    ShowError("document is not selected");
                    return;
                }
                db.GetDocument(docID).Delete();
                itemList.Items.Remove(itemList.SelectedItem);
                docIdList.Items.Remove(docID);
                docInfo.Text = "";
            }
            catch (Exception ex)
            {
                ShowError("DeleteButton_Click: "+ ex.Message);
            }
        }

        private void DeleteAttachemtButton_Click(object sender, RoutedEventArgs e)
        {
            var docID = "";
            if ((itemList.SelectedItem as Doc) != null)
                docID = (itemList.SelectedItem as Doc).ID;
            else docID = docIdList.SelectedItem as string ?? "";
            if (docID == "")
            {
                ShowError("document is not selected");
                return;
            }
            var doc = db.GetDocument(docID);
            var newRev = doc.CurrentRevision.CreateRevision();
            newRev.RemoveAttachment("binaryData");
            var savedRev = newRev.Save();
        }

        private void ConnectBT_Click(object sender, RoutedEventArgs e)
        {
            ConnectBT.IsEnabled = false;
            txtPort.IsEnabled = false;
            txtIP.IsEnabled = false;
            StartReplications();
            //new System.Threading.Thread(() => StartReplications())
            //{
            //    IsBackground = true
            //}.Start();
            //StartReplications();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}