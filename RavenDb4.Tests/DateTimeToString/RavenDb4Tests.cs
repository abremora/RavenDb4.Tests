using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using System;
using System.Linq;
using Xunit;

// Server version 4.0.6

//<Project Sdk = "Microsoft.NET.Sdk" >
//  < PropertyGroup >
//    < TargetFramework > netcoreapp2.1</TargetFramework>
//    <IsPackable>false</IsPackable>
//  </PropertyGroup>
//  <ItemGroup>
//    <PackageReference Include = "Microsoft.NET.Test.Sdk" Version="15.8.0" />
//    <PackageReference Include = "RavenDB.Client" Version="4.0.6" />
//    <PackageReference Include = "xunit" Version="2.4.0" />
//    <PackageReference Include = "xunit.runner.visualstudio" Version="2.4.0">
//      <PrivateAssets>all</PrivateAssets>
//      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
//    </PackageReference>
//  </ItemGroup>
//</Project>

// Description
// DateTime.ToString(string format) does not work in projections


namespace RavenDb4.Tests.DateTimeToString {
    public class Booking {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Start { get; set; }
    }
    
    public class BookingIndex : AbstractIndexCreationTask<Booking> {
        public class Result {
            public string Id { get; set; }
            public string FullName { get; set; }
            public DateTime Start { get; set; }
        }

        public BookingIndex() {
            Map = bookings => from booking in bookings
                              select new Result {
                                  Id = booking.Id,
                                  FullName = booking.FirstName + " " + booking.LastName,
                                  Start = booking.Start,
                              };


            StoreAllFields(FieldStorage.Yes);
        }        
    }
    
    public class RavenTestBase {
        public static void SetupStore(Action<IDocumentStore> action) {
            var database = "UnitTest_" + Guid.NewGuid().ToString();
            var url = "http://127.0.0.1:8081";
            IDocumentStore store = null;

            try {
                store = CreateStore(url, database);          

                action(store);
            }
            finally {
                store?.Maintenance.Server.Send(new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters() {
                    DatabaseNames = new[] { database },
                    HardDelete = true
                }));
            }
        }

        public static IDocumentStore CreateStore(string url, string database, bool useOptimisticConcurrency = false) {
            var store = new DocumentStore {
                Urls = new[] { url },
                Database = database,
                Conventions = {
                    FindCollectionName = t => t.Name,
                    UseOptimisticConcurrency = useOptimisticConcurrency
                }
            };

            store.Initialize();
            store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(database)));

            return store;
        }
    }

    public class RavenDb4Tests : RavenTestBase {    
        // WORKS!
        [Fact]
        public void SearchBooking_ProjectionWithDateTimeToString_ReturnsResult() {
            SetupStore(store => {             
                store.ExecuteIndex(new BookingIndex());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndex.Result, BookingIndex>()
                        .Where(x => x.FullName == "Alex Me")
                        .Select(x => new {
                            FullName = x.FullName,
                            FirstName = x.FullName.Split(new[] { ' ' })[0],
                            StartDate = x.Start.ToString()
                        })
                        .Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal("Alex", result.FirstName);
                    Assert.Equal("Alex Me", result.FullName);
                }
            });
        }

        // FAIL!
        // Message: System.NotSupportedException : Could not understand expression: 
        // from index 'BookingIndex'.Where(x => (x.FullName == "Alex Me")).Select(x => new BookingReadModel() { FullName = x.FullName, StartDate = x.Start.ToString("dd.MM.yyyy")}).Single()
        //---- System.NotSupportedException : By default, Lambda2Js cannot convert custom instance methods, only static ones. `ToString` is not static.
        //
        // My note: string.ToString() is not a static method. Right. 
        // But string.ToString() is working. string.ToString(string format) does not work.
        // string.Split() is not a static method, too. But it also works.
        [Fact]
        public void SearchBooking_ProjectionWithDateTimeToStringAndFormat_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndex());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndex.Result, BookingIndex>()
                        .Where(x => x.FullName == "Alex Me")
                        .Select(x => new  {
                            FullName = x.FullName,
                            StartDate = x.Start.ToString("dd.MM.yyyy")
                        })
                        .Single();

                    // Assert
                    Assert.NotNull(result);                    
                }
            });
        }
    }   
}
