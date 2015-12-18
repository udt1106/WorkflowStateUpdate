using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Layouts;
using Sitecore.Workflows;
using Sitecore.Workflows.Simple;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkflowStateUpdate
{
    public class WorkflowStateUpdate
    {
        // List all controls in page item
        public RenderingReference[] GetListOfSublayouts(string itemId, Item targetItem)
        {
            RenderingReference[] renderings = null;

            if (Sitecore.Data.ID.IsID(itemId))
            {
                renderings = targetItem.Visualization.GetRenderings(Sitecore.Context.Device, true);
            }
            return renderings;
        }

        // Return all datasource defined on one item
        public IEnumerable<string> GetDatasourceValue(WorkflowPipelineArgs args, Item targetItem)
        {
            List<string> uniqueDatasourceValues = new List<string>();
            Sitecore.Layouts.RenderingReference[] renderings = GetListOfSublayouts(targetItem.ID.ToString(), targetItem);
            foreach (var rendering in renderings)
            {
                if (!uniqueDatasourceValues.Contains(rendering.Settings.DataSource))
                    uniqueDatasourceValues.Add(rendering.Settings.DataSource);
            }

            return uniqueDatasourceValues;
        }

        // Check workflow state and update state
        public WorkflowResult ChangeWorkflowState(Item item, ID workflowStateId)
        {
            using (new EditContext(item))
            {
                item[FieldIDs.WorkflowState] = workflowStateId.ToString();
            }

            Sitecore.Layouts.RenderingReference[] renderings = GetListOfSublayouts(item.ID.ToString(), item);
            return new WorkflowResult(true, "OK", workflowStateId);
        }

        // Verify workflow state and update workflow state
        public WorkflowResult ChangeWorkflowState(Item item, string workflowStateName)
        {
            IWorkflow workflow = item.Database.WorkflowProvider.GetWorkflow(item);
            if (workflow == null)
            {
                return new WorkflowResult(false, "No workflow assigned to item");
            }

            WorkflowState newState = workflow.GetStates().FirstOrDefault(state => state.DisplayName == workflowStateName);

            if (newState == null)
            {
                return new WorkflowResult(false, "Cannot find workflow state " + workflowStateName);
            }

            unlockItem(newState, item);
            if (item.HasChildren)
            {
                // First child
                foreach (Item child1 in item.Children)
                {
                    using (new EditContext(child1))
                    {
                        child1[FieldIDs.WorkflowState] = ID.Parse(newState.StateID).ToString();
                        unlockItem(newState, child1);
                    }

                    // Second Child
                    if (child1.HasChildren)
                    {
                        foreach (Item child2 in child1.Children)
                        {
                            using (new EditContext(child2))
                            {
                                child2[FieldIDs.WorkflowState] = ID.Parse(newState.StateID).ToString();
                                unlockItem(newState, child2);
                            }

                            // Third Child
                            if (child2.HasChildren)
                            {
                                foreach (Item child3 in child2.Children)
                                {
                                    using (new EditContext(child3))
                                    {
                                        child3[FieldIDs.WorkflowState] = ID.Parse(newState.StateID).ToString();
                                        unlockItem(newState, child3);
                                    }
                                }
                            } // End Third Child
                        }
                    } // End Second Child
                }
            }
            return ChangeWorkflowState(item, ID.Parse(newState.StateID));
        }

        // Unlock the item when it is on FinalState
        public void unlockItem(WorkflowState newState, Item item)
        {
            if (newState.FinalState)
            {
                imageFieldPublishing(item);
            }
            if (newState.FinalState && item.Locking.IsLocked())
            {
                using (new EditContext(item, false, false))
                {
                    item["__lock"] = "<r />";
                }
            }
        }

        // Publish media item
        public void imageFieldPublishing(Item item)
        {
            foreach (Field field in item.Fields)
            {
                if (FieldTypeManager.GetField(field) is ImageField && !String.IsNullOrEmpty(field.Value))
                {
                    ImageField imagePath = field;
                    Item mediaItem = imagePath.MediaItem.Paths.Item;

                    // Publish image item
                    using (new Sitecore.SecurityModel.SecurityDisabler())
                    {
                        Database source = Sitecore.Configuration.Factory.GetDatabase("master");
                        Database target = Sitecore.Configuration.Factory.GetDatabase("web");
                        var options = new Sitecore.Publishing.PublishOptions(source, target,
                                                            Sitecore.Publishing.PublishMode.SingleItem, mediaItem.Language,
                                                            DateTime.Now)
                        {
                            RootItem = mediaItem,
                            Deep = false,
                        };
                        var publisher = new Sitecore.Publishing.Publisher(options);
                        publisher.PublishAsync();
                    }
                }
            }
        }
    }
}
