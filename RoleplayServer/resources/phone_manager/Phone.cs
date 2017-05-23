﻿using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RoleplayServer.resources.database_manager;
using RoleplayServer.resources.inventory;

namespace RoleplayServer.resources.phone_manager
{
    public class Phone : IInventoryItem
    {
        #region InvRelated Stuff

        [BsonId]
        public ObjectId Id { get; set; }

        public int Amount { get; set; }

        public int AmountOfSlots => 25; //TODO: Change this to something apporpriate.

        public bool CanBeDropped => true;
        public bool CanBeGiven => true;
        public bool CanBeStacked => false;
        public bool CanBeStashed => true;
        public bool IsBlocking => false;
        public int MaxAmount => 1;

        public string CommandFriendlyName => "phone_" + PhoneName;
        public string LongName => "Phone " + PhoneName;
        public int Object => 0;

        #endregion

        public string Number { get; set; }
        public bool IsOn { get; set; }
        public string PhoneName { get; set; }

        [BsonIgnore]
        public List<PhoneContact> Contacts = new List<PhoneContact>();

        public Phone()
        {
            Number = "0";
            IsOn = true;
            PhoneName = "Phone";
        }

        public static void InsertNumber(ObjectId phoneid, string num)
        {
            PhoneNumber number = new PhoneNumber()
            {
                Number = num,
                PhoneId = phoneid
            };
            DatabaseManager.PhoneNumbersTable.InsertOne(number);
        }

        public static void ChangeNumber(ObjectId phoneid, string newNumber)
        {
            PhoneNumber number = new PhoneNumber()
            {
                Number = newNumber,
                PhoneId = phoneid
            };
            DatabaseManager.PhoneNumbersTable.ReplaceOneAsync(y => y.PhoneId == phoneid, number);
        }

        /* ============== CONTACTS ================ */

        public void InsertContact(string name, string number)
        {
            var contact = new PhoneContact
            {
                Name = name,
                Number = number,
                PhoneId = Id.ToString()
            };

            contact.Insert();
            Contacts.Add(contact);
        }

        public void DeleteContact(PhoneContact contact)
        {
            Contacts.Remove(contact);
            var filter = Builders<PhoneContact>.Filter.Eq("Id", contact.Id);
            DatabaseManager.ContactTable.DeleteOneAsync(filter);
        }

        public void DeleteAllContacts()
        {
            Contacts.Clear();
            var filter = Builders<PhoneContact>.Filter.Eq("PhoneId", Id.ToString());
            DatabaseManager.ContactTable.DeleteManyAsync(filter);
        }

        public void LoadContacts()
        {
            var filter = Builders<PhoneContact>.Filter.Eq("PhoneId", Id.ToString());
            Contacts = DatabaseManager.ContactTable.Find(filter).ToList();
        }

        public bool HasContactWithName(string name)
        {
            return Contacts.Count(pc => string.Equals(pc.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        public bool HasContactWithNumber(string number)
        {
            return Contacts.Count(pc => pc.Number == number) > 0;
        }

        public bool HasContact(string name, string number)
        {
            return Contacts.Count(pc => pc.Number == number || string.Equals(pc.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        }


        /* ============== TEXT MESSAGES =============*/
        //NOTE: These are static methods and are not linked to a phone basically.. for performance.

        public long GetMessageCount(string from, string to)
        {
            var filter = Builders<PhoneMessage>.Filter.Eq(x => x.SenderNumber, from);
            filter = filter & Builders<PhoneMessage>.Filter.Eq(x => x.ToNumber, to);
            return DatabaseManager.MessagesTable.Find(filter).Count();
        }

        public static void LogMessage(string from, string to, string message)
        {
            var msg = new PhoneMessage()
            {
                Message = message,
                DateSent = DateTime.Now,
                SenderNumber = from,
                ToNumber = to,
                IsRead = false
            };
            msg.Insert();
        }

        public static List<PhoneMessage> GetMessageLog(string contact1, string contact2, int limit = 20, int toBeSkipped = 0)
        {
            var filter = (Builders<PhoneMessage>.Filter.Eq(x => x.SenderNumber, contact1) & Builders<PhoneMessage>.Filter.Eq(x => x.ToNumber, contact2)) | (Builders<PhoneMessage>.Filter.Eq(x => x.SenderNumber, contact2) & Builders<PhoneMessage>.Filter.Eq(x => x.ToNumber, contact1));
            var sort = Builders<PhoneMessage>.Sort.Descending(x => x.DateSent);
            var messages = DatabaseManager.MessagesTable.Find(filter).Sort(sort).Skip(toBeSkipped).Limit(limit).ToList();
            return messages;
        }

        public static List<string[]> GetContactListOfMessages(string number)
        {
            var filter = Builders<PhoneMessage>.Filter.Eq(x => x.ToNumber, number);
            var sort = Builders<PhoneMessage>.Sort.Descending(x => x.DateSent);
            var numbersList = DatabaseManager.MessagesTable.Find(filter).Sort(sort).Project(x => new [] { x.SenderNumber, x.Message, x.DateSent.ToString("g"),x.IsRead.ToString() }).ToEnumerable();
            List<string[]> numbers = new List<string[]>();
            foreach (var itm in numbersList)
            {
                if (numbers.SingleOrDefault(x => x[0] == itm[0]) == null)
                {
                    numbers.Add(itm);
                }
            }
            return numbers;
        }
        /* ============== PHONE CALLS ==============*/
    }
}