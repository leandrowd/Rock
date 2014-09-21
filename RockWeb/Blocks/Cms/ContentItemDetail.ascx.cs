﻿// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using Rock;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using Attribute = Rock.Model.Attribute;
using System.ComponentModel;
using Rock.Security;
using Newtonsoft.Json;
using Rock.Web;

namespace RockWeb.Blocks.Cms
{
    /// <summary>
    /// 
    /// </summary>
    [DisplayName("Content Item Detail")]
    [Category("CMS")]
    [Description("Displays the details for a content item.")]
    public partial class ContentItemDetail : RockBlock, IDetailBlock
    {

        #region Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                ShowDetail( PageParameter( "contentItemId" ).AsInteger(), PageParameter("contentChannelId").AsIntegerOrNull() );
            }
            else
            {
                var rockContext = new RockContext();
                ContentItem item;
                int? itemId = PageParameter( "contentItemId" ).AsIntegerOrNull();
                if ( itemId.HasValue && itemId.Value > 0 )
                {
                    item = new ContentItemService( rockContext ).Get( itemId.Value );
                }
                else
                {
                    item = new ContentItem { Id = 0, ContentChannelId = PageParameter("contentChannelId").AsInteger() };
                }
                item.LoadAttributes();
                phAttributes.Controls.Clear();
                Rock.Attribute.Helper.AddEditControls( item, phAttributes, false );
            }
        }

        /// <summary>
        /// Gets the bread crumbs.
        /// </summary>
        /// <param name="pageReference">The page reference.</param>
        /// <returns></returns>
        public override List<BreadCrumb> GetBreadCrumbs( PageReference pageReference )
        {
            var breadCrumbs = new List<BreadCrumb>();

            int? contentItemId = PageParameter( pageReference, "contentItemId" ).AsIntegerOrNull();
            if ( contentItemId != null )
            {
                ContentItem contentItem = new ContentItemService( new RockContext() ).Get( contentItemId.Value );
                if ( contentItem != null )
                {
                    breadCrumbs.Add( new BreadCrumb( contentItem.Title, pageReference ) );
                }
                else
                {
                    breadCrumbs.Add( new BreadCrumb( "New Content Item", pageReference ) );
                }
            }
            else
            {
                // don't show a breadcrumb if we don't have a pageparam to work with
            }

            return breadCrumbs;
        }
        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the lbSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void lbSave_Click( object sender, EventArgs e )
        {
            var rockContext = new RockContext();
            ContentItem contentItem = null;

            ContentItemService contentItemService = new ContentItemService( rockContext );

            int contentItemId = hfContentItemId.Value.AsInteger();
             
            if ( contentItemId == 0 )
            {
                var contentChannel = new ContentChannelService( rockContext ).Get( hfContentChannelId.Value.AsInteger() );
                if ( contentChannel != null )
                {
                    contentItem = new ContentItem
                    {
                        ContentChannel = contentChannel,
                        ContentChannelId = contentChannel.Id,
                        ContentType = contentChannel.ContentType,
                        ContentTypeId = contentChannel.ContentType.Id,
                        Status = ( contentChannel.RequiresApproval ? ContentItemStatus.PendingApproval : ContentItemStatus.Approved ),
                        ApprovedByPersonAliasId = ( contentChannel.RequiresApproval ? (int?)null : CurrentPersonAliasId )
                    };
                    contentItemService.Add( contentItem );
                }
            }
            else
            {
                contentItem = contentItemService.Get( contentItemId );
            }

            if ( contentItem != null )
            {
                contentItem.Title = tbTitle.Text;
                contentItem.Content = ceContent.Text;
                contentItem.StartDateTime = dtpStart.SelectedDateTime ?? RockDateTime.Now;
                contentItem.ExpireDateTime = dtpExpire.SelectedDateTime;

                contentItem.LoadAttributes( rockContext );
                Rock.Attribute.Helper.GetEditValues( phAttributes, contentItem );

                if ( !Page.IsValid || !contentItem.IsValid )
                {
                    // Controls will render the error messages                    
                    return;
                }

                rockContext.WrapTransaction( () =>
                {
                    rockContext.SaveChanges();
                    contentItem.SaveAttributeValues( rockContext );
                } );

                ReturnToParentPage();
            }

        }

        /// <summary>
        /// Handles the Click event of the lbCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void lbCancel_Click( object sender, EventArgs e )
        {
            ReturnToParentPage();
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            ShowDetail( hfContentItemId.ValueAsInt() );
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the type of the content.
        /// </summary>
        /// <param name="contentItemId">The content type identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private ContentItem GetContentItem( int contentItemId, RockContext rockContext = null )
        {
            rockContext = rockContext ?? new RockContext();
            var contentItem = new ContentItemService( rockContext )
                .Queryable( "ContentChannel,ContentType" )
                .Where( t => t.Id == contentItemId )
                .FirstOrDefault();
            return contentItem;
        }

        /// <summary>
        /// Shows the detail.
        /// </summary>
        /// <param name="contentItemId">The marketing campaign ad type identifier.</param>
        public void ShowDetail( int contentItemId )
        {
            ShowDetail( contentItemId, null );
        }

        public void ShowDetail( int contentItemId, int? contentChannelId )
        {
            ContentItem contentItem = null;

            bool canEdit = IsUserAuthorized( Authorization.EDIT );

            var rockContext = new RockContext();

            if ( !contentItemId.Equals( 0 ) )
            {
                contentItem = GetContentItem( contentItemId );
            }

            if ( contentItem == null && contentChannelId.HasValue )
            {
                var contentChannel = new ContentChannelService(rockContext).Get( contentChannelId.Value );
                if (contentChannel != null)
                {
                    contentItem = new ContentItem { 
                        Id = 0, 
                        StartDateTime = RockDateTime.Now,
                        ContentChannel = contentChannel,
                        ContentChannelId = contentChannel.Id,
                        ContentType = contentChannel.ContentType,
                        ContentTypeId = contentChannel.ContentType.Id
                    };
                }
            }

            if ( contentItem != null && ( canEdit || contentItem.IsAuthorized( Authorization.EDIT, CurrentPerson ) ) ) 
            {

                pnlEditDetails.Visible = true;

                hfContentItemId.Value = contentItem.Id.ToString();
                hfContentChannelId.Value = contentItem.ContentChannelId.ToString();

                string cssIcon = contentItem.ContentChannel.IconCssClass;
                if ( string.IsNullOrWhiteSpace( cssIcon ) )
                {
                    cssIcon = "fa fa-certificate";
                }
                lIcon.Text = string.Format( "<i class='{0}'></i>", cssIcon );

                string title = contentItem.Id > 0 ?
                    ActionTitle.Edit( ContentItem.FriendlyTypeName ) :
                    ActionTitle.Add( ContentItem.FriendlyTypeName );
                lTitle.Text = title.FormatAsHtmlTitle();

                hlContentChannel.Text = contentItem.ContentChannel.Name;
                hlStatus.Text = contentItem.Status.ConvertToString();

                if ( contentItem.Status == ContentItemStatus.Approved )
                {
                    hlStatus.LabelType = LabelType.Success;
                }
                else if ( contentItem.Status == ContentItemStatus.Denied )
                {
                    hlStatus.LabelType = LabelType.Danger;
                }
                else
                {
                    hlStatus.LabelType = LabelType.Default;
                }

                tbTitle.Text = contentItem.Title;
                ceContent.Text = contentItem.Content;
                dtpStart.SelectedDateTime = contentItem.StartDateTime;
                dtpExpire.SelectedDateTime = contentItem.ExpireDateTime;

                contentItem.LoadAttributes();
                phAttributes.Controls.Clear();
                Rock.Attribute.Helper.AddEditControls( contentItem, phAttributes, true );
            }
            else
            {
                nbEditModeMessage.Text = EditModeMessage.NotAuthorizedToView( ContentChannel.FriendlyTypeName );
                pnlEditDetails.Visible = false;
            }
        }

        /// <summary>
        /// Returns to parent page.
        /// </summary>
        private void ReturnToParentPage()
        {
            var qryParams = new Dictionary<string,string>();
            qryParams.Add( "contentChannelId", hfContentChannelId.Value );
            NavigateToParentPage( qryParams );
        }

        #endregion


}
}