using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using SuperSimple.Auth;
using MongoDB.Bson;
using MtgDb.Info.Driver;
using System.Configuration;
using System.Linq;

namespace MtgDb.Info
{
    public class MongoRepository : IRepository
    {
        private string Connection { get; set; }
        private MongoDatabase database;
        private MongoClient client;
        private MongoServer server;

        private SuperSimpleAuth ssa;

        public Db magicdb;

        public MongoRepository (string connection)
        {
            ssa = new SuperSimpleAuth (
                ConfigurationManager.AppSettings.Get("domain"), 
                ConfigurationManager.AppSettings.Get("domain_key")); 

            magicdb =       new Db (ConfigurationManager.AppSettings.Get("api"));
            Connection =    connection;
            client =        new MongoClient(connection);
            server =        client.GetServer();
            database =      server.GetDatabase("mtgdb_info");
            CreateUserCardIndexes ();
        }

        public MongoRepository (string connection, Db mtgdb, SuperSimpleAuth auth)
        {
            Connection =    connection;
            magicdb =       mtgdb;
            //magicdb =  new Db (ConfigurationManager.AppSettings.Get("api"));
            client =        new MongoClient(connection);
            server =        client.GetServer();
            database =      server.GetDatabase("mtgdb_info");
            ssa =           auth;
            CreateUserCardIndexes ();
        }

        public Guid AddCard(NewCard card)
        {
            MongoCollection<NewCard> collection = 
                database.GetCollection<NewCard> ("new_cards");

            card.Id = Guid.NewGuid();
            card.Status = "Pending";
            card.CreatedAt = DateTime.Now;

            collection.Save(card);

            return card.Id;
        }

        public NewCard UpdateNewCardStatus(Guid id, string status)
        {
            MongoCollection<NewCard> collection = 
                database.GetCollection<NewCard> ("new_cards");

            var query =         Query<NewCard>.EQ (e => e.Id, id);
            NewCard card =      collection.FindOne(query);
            card.Status =       status;
            card.ModifiedAt =   DateTime.Now;

            collection.Save(card);

            return card;
        }
            
        public NewCard GetCard(Guid id)
        {
            MongoCollection<NewCard> collection = 
                database.GetCollection<NewCard> ("new_cards");

            var query = Query.And(Query<NewCard>.EQ (e => e.Id, id ));
            NewCard card =  collection.FindOne(query);

            return card;
        }

        public NewCard [] GetNewCards(string status = null)
        {
            MongoCollection<NewCard> collection = 
                database.GetCollection<NewCard> ("new_cards");

            IOrderedEnumerable<NewCard> mongoResults;
            List<NewCard> cards = new List<NewCard>(); 

            if(status == null)
            {
                mongoResults = collection.FindAll()
                    .OrderByDescending(x => x.CreatedAt);
            }
            else
            {
                var query = Query.And(Query<NewCard>.EQ (e => e.Status, status ));

                mongoResults = collection.Find(query)
                    .OrderByDescending(x => x.CreatedAt);
            }

            foreach(NewCard c in mongoResults)
            {
                c.User = GetProfile(c.UserId);
                cards.Add(c);
            }

            return cards.ToArray();
        }
            
        public CardChange[] GetChangeRequests(string status = null)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            List<CardChange> changes = new List<CardChange>(); 
            IOrderedEnumerable<CardChange> mongoResults;

            if(status == null)
            {
                mongoResults = collection.FindAll()
                    .OrderByDescending(x => x.CreatedAt);
            }
            else
            {
                var query = Query.And(Query<CardChange>.EQ (e => e.Status, status),
                    Query<CardChange>.NE (e => e.Version, 0));

                mongoResults = collection.Find(query)
                    .OrderByDescending(x => x.CreatedAt);
            }

            foreach(CardChange c in mongoResults)
            {
                c.User = GetProfile(c.UserId);
                changes.Add(c);
            }

            return changes.ToArray();
        }

        public Guid AddSet(NewSet set)
        {
            MongoCollection<NewSet> collection = 
                database.GetCollection<NewSet> ("new_sets");

            set.Id = Guid.NewGuid();
            set.Status = "Pending";
            set.CreatedAt = DateTime.Now;

            collection.Save(set);

            return set.Id;
        }

        public NewSet GetSet(Guid id)
        {
            MongoCollection<NewSet> collection = 
                database.GetCollection<NewSet> ("new_sets");

            var query = Query.And(Query<NewSet>.EQ (e => e.Id, id ));
            NewSet set =  collection.FindOne(query);
        
            return set;
        }

        public NewSet [] GetNewSets(string status = null)
        {
            MongoCollection<NewSet> collection = 
                database.GetCollection<NewSet> ("new_sets");

            IOrderedEnumerable<NewSet> mongoResults;
            List<NewSet> sets = new List<NewSet>(); 

            if(status == null)
            {
                mongoResults = collection.FindAll()
                    .OrderByDescending(x => x.CreatedAt);
            }
            else
            {
                var query = Query.And(Query<NewSet>.EQ (e => e.Status, status ));

                mongoResults = collection.Find(query)
                    .OrderByDescending(x => x.CreatedAt);
            }

            foreach(NewSet s in mongoResults)
            {
                s.User = GetProfile(s.UserId);
                sets.Add(s);
            }

            return sets.ToArray();
        }

        public NewSet UpdateNewSetStatus(Guid id, string status)
        {
            MongoCollection<NewSet> collection = 
                database.GetCollection<NewSet> ("new_sets");

            var query =        Query<NewSet>.EQ (e => e.Id, id);
            NewSet set =       collection.FindOne(query);
            set.Status =       status;
            set.ModifiedAt =   DateTime.Now;

            collection.Save(set);

            return set;
        }

        public Guid AddCardSetChangeRequest(SetChange change)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            List<SetChange> changes =   new List<SetChange>(); 
            CardSet set =               magicdb.GetSet(change.SetId);
            var query =                 Query.And(Query<SetChange>.EQ (e => e.SetId, change.SetId ));
            var mongoResults =          collection.Find(query);

            foreach(SetChange c in mongoResults)
            {
                changes.Add(c);
            }

            if(changes.Count == 0)
            {
                SetChange original =   SetChange.MapSet(set);
                original.Comment =      "Original set info";
                original.Id =           Guid.NewGuid();
                original.Version =      0;
                original.CreatedAt =    DateTime.Now;

                collection.Save(original);
            }

            change.Id =             Guid.NewGuid();
            change.Version =        changes.Count == 0 ? 1 : changes.Count;
            change.CreatedAt =      DateTime.Now;
            change.ModifiedAt =     DateTime.Now;
            change.FieldsUpdated  = SetChange.FieldsChanged(set, change);
            change.Status = "Pending";

            if(change.FieldsUpdated == null ||
                change.FieldsUpdated.Length == 0)
            {
                throw new ArgumentException("No fields have been updated.");
            }

            try
            {
                collection.Save(change);
            }
            catch(Exception e)
            {
                change.Id = Guid.Empty;
                throw e; 
            }

            return change.Id;
        }

        public SetChange GetCardSetChangeRequest(Guid id)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            var query = Query.And(Query<SetChange>.EQ (e => e.Id, id ));
            SetChange change =  collection.FindOne(query);
            if(change.Version != 0)
            {
                change.User = GetProfile(change.UserId);
            }

            return change;
        }

        public SetChange[] GetCardSetChangeRequests(string setId)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            List<SetChange> changes = new List<SetChange>(); 

            var query = Query.And(Query<SetChange>.EQ (e => e.SetId, setId.ToUpper()));
            var mongoResults = collection.Find(query);

            foreach(SetChange c in mongoResults)
            {
                c.User = GetProfile(c.UserId);
                changes.Add(c);
            }

            return changes.ToArray();
        }

        public SetChange[] GetSetChangeRequests(string status = null)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            List<SetChange> changes = new List<SetChange>(); 
            IOrderedEnumerable<SetChange> mongoResults;

            if(status == null)
            {
                mongoResults = collection.FindAll()
                    .OrderByDescending(x => x.CreatedAt);
            }
            else
            {
                var query = Query.And(Query<SetChange>.EQ (e => e.Status, status),
                    Query<SetChange>.NE (e => e.Version, 0));

                mongoResults = collection.Find(query)
                    .OrderByDescending(x => x.CreatedAt);
            }

            foreach(SetChange c in mongoResults)
            {
                c.User = GetProfile(c.UserId);
                changes.Add(c);
            }

            return changes.ToArray();
        }

        public SetChange UpdateCardSetChangeStatus(Guid id, 
            string status, string field = null)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            var query =                 Query<SetChange>.EQ (e => e.Id, id);
            SetChange change =          collection.FindOne(query);
            change.Status =             status;
            change.ModifiedAt =         DateTime.Now;
            List<string> committed  =   new List<string>();

            if(field != null)
            {
                if(change.ChangesCommitted == null)
                {
                    committed.Add(field);
                }
                else
                {
                    committed = change.ChangesCommitted.ToList();
                    committed.Add(field);
                }

                change.ChangesCommitted = committed.ToArray();
            }

            if(change.Version > 0)
            {
                collection.Save(change);
            }

            if(field != null)
            {
                OverwrittenSetField(change.Id, change.SetId, field);
            }

            return change;
        }
            
        public CardChange UpdateCardChangeStatus(Guid id, 
            string status, string field = null)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            var query =                 Query<CardChange>.EQ (e => e.Id, id);
            CardChange change =         collection.FindOne(query);
            change.Status =             status;
            change.ModifiedAt =         DateTime.Now;
            List<string> committed  =   new List<string>();


            if(field != null)
            {
                if(change.ChangesCommitted == null)
                {
                    committed.Add(field);
                }
                else
                {
                    committed = change.ChangesCommitted.ToList();
                    committed.Add(field);
                }

                change.ChangesCommitted = committed.ToArray();
            }

            if(change.Version > 0)
            {
                collection.Save(change);
            }

            if(field != null)
            {
                OverwrittenField(change.Id, change.Mvid, field);
            }

            return change;
        }

        private void OverwrittenField(Guid changeId, int mvid, string field)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            CardChange[] changes = GetCardChangeRequests(mvid);

            if(changes != null && changes.Length > 0)
            {
                changes = changes.Where(x => x.Status == "Accepted").ToArray();

                foreach(CardChange change in changes)
                {
                    if(change.Id != changeId)
                    {
                        foreach(string f in change.ChangesCommitted)
                        {
                            if(f == field)
                            {
                                List<string> over = new List<string>();
                                if(change.ChangesOverwritten != null)
                                {
                                    over = change.ChangesOverwritten.ToList();
                                }

                                if(!over.Exists(x => x == field))
                                {
                                    over.Add(field);
                                    change.ChangesOverwritten = over.ToArray();
                                }
                            }
                        }

                        collection.Save(change);
                    }
                }
            }
        }

        private void OverwrittenSetField(Guid changeId, string setId, string field)
        {
            MongoCollection<SetChange> collection = 
                database.GetCollection<SetChange> ("set_changes");

            SetChange[] changes = GetCardSetChangeRequests(setId);

            if(changes != null && changes.Length > 0)
            {
                changes = changes.Where(x => x.Status == "Accepted").ToArray();

                foreach(SetChange change in changes)
                {
                    if(change.Id != changeId)
                    {
                        foreach(string f in change.ChangesCommitted)
                        {
                            if(f == field)
                            {
                                List<string> over = new List<string>();
                                if(change.ChangesOverwritten != null)
                                {
                                    over = change.ChangesOverwritten.ToList();
                                }

                                if(!over.Exists(x => x == field))
                                {
                                    over.Add(field);
                                    change.ChangesOverwritten = over.ToArray();
                                }
                            }
                        }

                        collection.Save(change);
                    }
                }
            }
        }
            
        public CardChange GetCardChangeRequest(Guid id)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            var query = Query.And(Query<CardChange>.EQ (e => e.Id, id ));
            CardChange change =  collection.FindOne(query);
            if(change.Version != 0)
            {
                change.User = GetProfile(change.UserId);
            }

            return change;
        }

        public CardChange[] GetCardChangeRequests(int mvid)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            List<CardChange> changes = new List<CardChange>(); 

            var query = Query.And(Query<CardChange>.EQ (e => e.Mvid, mvid ));
            var mongoResults = collection.Find(query);

            foreach(CardChange c in mongoResults)
            {
                c.User = GetProfile(c.UserId);
                changes.Add(c);
            }
                
            return changes.ToArray();
        }

        public Guid AddCardChangeRequest(CardChange change)
        {
            MongoCollection<CardChange> collection = 
                database.GetCollection<CardChange> ("card_changes");

            List<CardChange> changes =  new List<CardChange>(); 
            Card card =                 magicdb.GetCard(change.Mvid);
            var query =                 Query.And(Query<CardChange>.EQ (e => e.Mvid, change.Mvid ));
            var mongoResults =          collection.Find(query);

            foreach(CardChange c in mongoResults)
            {
                changes.Add(c);
            }

            if(changes.Count == 0)
            {
                CardChange original =   CardChange.MapCard(card);
                original.Comment =      "Original card info";
                original.Id =           Guid.NewGuid();
                original.Version =      0;
                original.CreatedAt =    DateTime.Now;

                collection.Save(original);
            }

            change.Id =             Guid.NewGuid();
            change.Version =        changes.Count == 0 ? 1 : changes.Count;
            change.CreatedAt =      DateTime.Now;
            change.ModifiedAt =     DateTime.Now;
            change.FieldsUpdated  = CardChange.FieldsChanged(card, change);
            change.Status = "Pending";

            if(change.FieldsUpdated == null ||
                change.FieldsUpdated.Length == 0)
            {
                throw new ArgumentException("No fields have been updated.");
            }

            try
            {
                collection.Save(change);
            }
            catch(Exception e)
            {
                change.Id = Guid.Empty;
                throw e; 
            }

            return change.Id;
        }

        public Dictionary<string, int> GetSetCardCounts(Guid walkerId)
        {
            Dictionary<string, int> userSets = new Dictionary<string, int> ();

            CardSet[] sets = magicdb.GetSets();
            UserCard[] users = null;

            foreach(CardSet s in sets)
            {
                users = GetUserCards (walkerId, s.CardIds);

                if(users != null && users.Length > 0)
                {
                    userSets.Add (s.Id, users.Length);
                }
            }

            return userSets;
        }

        public UserCard[] GetUserCards(Guid walkerId)
        {
            var collection = database.GetCollection<UserCard>("user_cards");
            List<UserCard> userCards = new List<UserCard> ();

            var query = Query.And(
                Query<UserCard>.EQ(c => c.PlaneswalkerId, walkerId),
                Query<UserCard>.GT(c => c.Amount, 0));

            var cards = collection.Find (query);

            foreach(UserCard c in cards)
            {
                userCards.Add (c);
            }

            return userCards.ToArray ();
        }

        public UserCard[] GetUserCards(Guid walkerId, int[] multiverseIds)
        {
            var collection = database.GetCollection<UserCard>("user_cards");
            List<UserCard> userCards = new List<UserCard> ();

            var query = Query.And(Query.In ("MultiverseId", new BsonArray(multiverseIds)),
                Query<UserCard>.EQ(c => c.PlaneswalkerId, walkerId),
                Query<UserCard>.GT(c => c.Amount, 0));

            var cards = collection.Find (query);

            foreach(UserCard c in cards)
            {
                userCards.Add (c);
            }

            return userCards.ToArray ();
        }

        public UserCard[] GetUserCards(Guid walkerId, string setId)
        {
            var collection = database.GetCollection<UserCard>("user_cards");
            List<UserCard> userCards = new List<UserCard> ();

            var query = Query.And(Query<UserCard>.EQ(c => c.SetId, setId.ToUpper()),
                Query<UserCard>.EQ(c => c.PlaneswalkerId, walkerId),
                Query<UserCard>.GT(c => c.Amount, 0));

            var cards = collection.Find (query);

            foreach(UserCard c in cards)
            {
                userCards.Add (c);
            }

            return userCards.ToArray ();
        }

        public UserCard AddUserCard(Guid walkerId, int multiverseId, int amount)
        {
            MongoCollection<UserCard> cards = database.GetCollection<UserCard> ("user_cards");
            var query = Query.And(Query<UserCard>.EQ (e => e.MultiverseId, multiverseId),
                Query<UserCard>.EQ (e => e.PlaneswalkerId, walkerId));

            UserCard card = cards.FindOne(query);

            if(card == null)
            {
                UserCard newCard = new UserCard ();
                newCard.Id = Guid.NewGuid ();
                newCard.Amount = amount;
                newCard.MultiverseId = multiverseId;
                newCard.PlaneswalkerId = walkerId;
                newCard.SetId = magicdb.GetCard (multiverseId).CardSetId;

                cards.Insert (newCard);
                card = newCard;
            }
            else
            {
                card.Amount = amount;
                cards.Save (card);
            }

            return card;
        }


        public Guid AddPlaneswalker(string userName, string password, string email)
        {
            var collection = database.GetCollection<Profile>("profiles");
          
            User user = ssa.CreateUser (userName, password, email);

            Profile profile = new Profile ();
            profile.Id = user.Id;
            profile.CreatedAt = DateTime.Now;
            profile.Email = user.Email;
            profile.Name = user.UserName;

            WriteConcernResult result = collection.Insert(profile);

            return user.Id;
        }

        public void RemovePlaneswalker(Guid id)
        {
            var collection = database.GetCollection<Profile> ("profiles");

            Dictionary<string, object> query = new Dictionary<string, object> ();
            query.Add ("_id", id);

            MongoCollection<UserCard> cards = database.GetCollection<UserCard> ("user_cards");
            var rmCards = Query<UserCard>.EQ (e => e.PlaneswalkerId, id);

            collection.Remove(new QueryDocument(query));
            cards.Remove (rmCards);
        }

        public Profile GetProfile(Guid id)
        {
            var collection = database.GetCollection<Profile> ("profiles");
            var query = Query<Profile>.EQ (e => e.Id, id);
            Profile p = collection.FindOne (query);

            if (p != null)
                return p;
      
            return null;
        }

        public Profile GetProfile(string name)
        {
            var collection = database.GetCollection<Profile> ("profiles");
            var query = Query<Profile>.EQ (e => e.Name, name);
            Profile p = collection.FindOne (query);

            if (p != null)
                return p;

            return null;
        }

        public Planeswalker UpdatePlaneswalker(Planeswalker planeswalker)
        {
            MongoCollection<Profile> walkers = database.GetCollection<Profile> ("profiles");
            var query = Query<Profile>.EQ (e => e.Id, planeswalker.Id);

            Profile updateWalker = walkers.FindOne(query);
            Planeswalker user = null;

            if (updateWalker != null) 
            {
                if(updateWalker.Email.ToLower() != planeswalker.Profile.Email.ToLower())
                {
                    ssa.ChangeEmail (planeswalker.AuthToken, planeswalker.Profile.Email);
                }

                if(updateWalker.Name.ToLower() != planeswalker.Profile.Name.ToLower())
                {
                    ssa.ChangeUserName (planeswalker.AuthToken, planeswalker.Profile.Name);
                }

                updateWalker.Email = planeswalker.Profile.Email;
                updateWalker.ModifiedAt = DateTime.Now;
                updateWalker.Name = planeswalker.Profile.Name;

                walkers.Save(updateWalker);

                User ssaUser = ssa.Validate (planeswalker.AuthToken);

                user = new Planeswalker 
                {
                    UserName = ssaUser.UserName,
                    AuthToken = ssaUser.AuthToken,
                    Email = ssaUser.Email,
                    Id = ssaUser.Id,
                    Claims = ssaUser.Claims,
                    Roles = ssaUser.Roles,
                    Profile = GetProfile(ssaUser.Id)
                };
            }
           
            return user;
        }

        private void CreateChangeRequestIndexes()
        {
            var keys = new IndexKeysBuilder();

            keys.Ascending("Id");

            var options = new IndexOptionsBuilder();
            options.SetSparse(true);
            options.SetUnique(true);

            var collection = database.GetCollection<CardChange>("card_changes");

            collection.EnsureIndex(keys, options);
        }

        private void CreateUserCardIndexes()
        {
            var keys = new IndexKeysBuilder();

            keys.Ascending("PlaneswalkerId","MultiverseId");

            var options = new IndexOptionsBuilder();
            options.SetSparse(true);
            options.SetUnique(true);

            var collection = database.GetCollection<UserCard>("user_cards");

            collection.EnsureIndex(keys, options);
        }
    }
}

