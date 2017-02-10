//-----------------------------------------------------------------------
// <copyright file="UserCache.cs" company="Jareth Development">
// The contents of these files can be freely used on any project without attribution
// </copyright>
//-----------------------------------------------------------------------
namespace PluginCacheExample
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Caches user information
    /// </summary>
    public class UserCache
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        private static UserCache instance;

        /// <summary>
        /// Date and time the cache was last reset
        /// </summary>
        private DateTime cacheLastReset;

        /// <summary>
        /// Cache record id
        /// </summary>
        private Guid cacheRecordId;

        /// <summary>
        /// The cache record name
        /// </summary>
        private const string cacheRecordName = "UserCache";

        /// <summary>
        /// Time the validty of the cache was last checked
        /// </summary>
        private DateTime timeLastChecked;

        /// <summary>
        /// Users cache - Using concurrent as multiple instances of plugin could be running
        /// </summary>
        private ConcurrentDictionary<Guid, Entity> users;

        /// <summary>
        /// Prevents a default instance of the <see cref="UserCache"/> class from being created.
        /// </summary>
        private UserCache()
        {
            this.users = new ConcurrentDictionary<Guid, Entity>();
        }

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static UserCache Instance
        {
            get
            {
                // If not initialised, set instance
                if (instance == null)
                {
                    instance = new UserCache();
                }

                return instance;
            }
        }

        /// <summary>
        /// Gets a user from the cache or CRM
        /// </summary>
        /// <param name="id">Id of the user</param>
        /// <param name="service">Organisation service</param>
        /// <returns>User entity</returns>
        public Entity GetUser(Guid id, IOrganizationService service)
        {
            this.CheckCacheValidity(service);

            // Check for user in cache
            if (this.users.ContainsKey(id))
            {
                return this.users[id];
            }

            // Get user from CRM
            var user = service.Retrieve("systemuser", id, new ColumnSet("domainname"));

            // Add to dictionary
            this.users.TryAdd(id, user);

            return user;
        }


        /// <summary>
        /// Resets the cache
        /// </summary>
        public void ResetCache()
        {
            this.users = new ConcurrentDictionary<Guid, Entity>();
        }

        /// <summary>
        /// Checks if the cache is valid
        /// </summary>
        /// <param name="service">Organisation service</param>
        private void CheckCacheValidity(IOrganizationService service)
        {
            if (timeLastChecked == null || timeLastChecked.AddMinutes(10) > DateTime.Now)
            {
                timeLastChecked = DateTime.Now;

                if (this.cacheRecordId == null)
                {
                    // If first time, find cache record

                    var query = new QueryExpression("jd_cache")
                    {
                        ColumnSet = new ColumnSet("jd_datelastrefreshed")
                    };

                    query.Criteria.AddCondition("jd_name", ConditionOperator.Equal, cacheRecordName);

                    var record = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                    // If record exists, setup properties
                    if (record != null)
                    {
                        this.cacheRecordId = record.Id;
                        this.cacheLastReset = (DateTime)record["jd_datelastrefreshed"];
                        this.ResetCache();
                    }
                }
                else
                {
                    // Otherwise get record from CRM
                    var record = service.Retrieve("jd_cache", this.cacheRecordId, new ColumnSet("jd_datelastrefreshed"));

                    if (record != null)
                    {
                        var date = (DateTime)record["jd_datelastrefreshed"];

                        if (date != this.cacheLastReset)
                        {
                            this.cacheLastReset = date;
                            this.ResetCache();
                        }
                    }
                }
            }
        }
    }
}
