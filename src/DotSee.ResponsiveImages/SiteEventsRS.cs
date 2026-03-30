//using Microsoft.Extensions.DependencyInjection;
//using Umbraco.Cms.Core.Cache;
//using Umbraco.Cms.Core.Composing;
//using Umbraco.Cms.Core.DependencyInjection;
//using Umbraco.Cms.Core.Events;
//using Umbraco.Cms.Core.Notifications;

//namespace DotSee.ResponsiveImages
//{
//    public class SiteEventsRSComposer : IComposer
//    {
// IF THIS CLASS IS EVER TO BE USED AGAIN, MOVE THE COMPOSE METHOD TO ResponsiveImagesServicesComposer.cs
//        public void Compose(IUmbracoBuilder builder)
//        {
//            builder.Services.AddSingleton<SubscribeToEventsRS>();
//        }
//    }

//    public class NodePublished : SubscribeToEventsRS, INotificationHandler<ContentPublishedNotification>
//    {
//        public NodePublished(AppCaches cache) : base(cache)
//        {
//        }

//        public void Handle(ContentPublishedNotification notification)
//        {
//            base.Handle();
//        }
//    }

//    public class NodeUnPublished : SubscribeToEventsRS, INotificationHandler<ContentUnpublishedNotification>
//    {
//        public NodeUnPublished(AppCaches cache) : base(cache)
//        {
//        }

//        public void Handle(ContentUnpublishedNotification notification)
//        {
//            base.Handle();
//        }
//    }

//    public class SubscribeToEventsRS
//    {
//        private readonly IAppPolicyCache _cache;

//        public SubscribeToEventsRS(AppCaches cache)
//        {
//            _cache = cache.RuntimeCache;
//        }

//        public void Handle()
//        {
//            ClearRuntimeCache();
//        }

//        private void ClearRuntimeCache()
//        {
//            _cache.ClearByKey(CacheLiteralsRS.CachedImageCss);
//            _cache.ClearByKey(CacheLiteralsRS.CachedImagesClassName);
//            _cache.ClearByKey(CacheLiteralsRS.Config);
//            _cache.ClearByKey(CacheLiteralsRS.Ruleset);
//        }
//    }

//}