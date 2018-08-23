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
// Projection with DateTime.ToString() fails for special combinations with Maps in index and stored fields in index

namespace RavenDb4.Tests.IndexProjection {
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


            //StoreAllFields(FieldStorage.Yes);
        }
    }

    public class BookingIndexFullNameIsFullNameMapStartToBegin : AbstractIndexCreationTask<Booking> {
        public class Result {
            public string Id { get; set; }
            public string FullName { get; set; }
            public DateTime Begin { get; set; }
        }

        public BookingIndexFullNameIsFullNameMapStartToBegin() {
            Map = bookings => from booking in bookings
                              select new Result {
                                  Id = booking.Id,
                                  FullName = booking.FirstName + " " + booking.LastName,
                                  Begin = booking.Start,
                              };

            //StoreAllFields(FieldStorage.Yes); // <- Uncomment this to fix the tests
        }
    }

    public class BookingIndexFullNameIsFirstNameMapStartToBegin : AbstractIndexCreationTask<Booking> {
        public class Result {
            public string Id { get; set; }
            public string FullName { get; set; }
            public DateTime Begin { get; set; }
        }

        public BookingIndexFullNameIsFirstNameMapStartToBegin() {
            Map = bookings => from booking in bookings
                              select new Result {
                                  Id = booking.Id,
                                  FullName = booking.FirstName,
                                  Begin = booking.Start,
                              };

            //StoreAllFields(FieldStorage.Yes); // <- Uncomment this to fix the tests
        }
    }

    public class BookingIndexFullNameIsFirstNameMapStartToStart : AbstractIndexCreationTask<Booking> {
        public class Result {
            public string Id { get; set; }
            public string FullName { get; set; }
            public DateTime Start { get; set; }
        }

        public BookingIndexFullNameIsFirstNameMapStartToStart() {
            Map = bookings => from booking in bookings
                              select new Result {
                                  Id = booking.Id,
                                  FullName = booking.FirstName,
                                  Start = booking.Start,
                              };

            //StoreAllFields(FieldStorage.Yes);
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
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToStart_ReturnsResult() {
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
                        .Select(x => new {
                            Start = x.Start.ToString(),
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Contains("2018", result.Start);
                }
            });
        }

        // FAIL!
        // Message: Raven.Client.Exceptions.Documents.Patching.JavaScriptException : 
        // Raven.Client.Exceptions.Documents.Patching.JavaScriptException: At 4:3 
        // {"name":"RangeError","callstack":" at toString() @  44:3\r\n"} ---> 
        //
        // Note: Use StoreAllFields(FieldStorage.Yes); in index to fix 
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToBegin_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFullNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFullNameMapStartToBegin.Result, BookingIndexFullNameIsFullNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex Me")
                        .Select(x => new {                           
                            Start = x.Begin.ToString(),
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Contains("2018", result.Start);
                }
            });
        }

        // WORKS!
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToBeginWithoutProjection_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFullNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFullNameMapStartToBegin.Result, BookingIndexFullNameIsFullNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex Me")
                        .OfType<Booking>()
                        .Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal("Alex", result.FirstName);
                }
            });
        }

        // WORKS!
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToBeginWithWrongConditions_ReturnsNoResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFullNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFullNameMapStartToBegin.Result, BookingIndexFullNameIsFullNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex")
                        .Select(x => new {
                            FullName = x.FullName,
                            Start = x.Begin.ToString(),
                        }).ToList();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Empty(result);
                }
            });
        }

        // FAIL!
        // Message: Raven.Client.Exceptions.Documents.Patching.JavaScriptException : 
        // Raven.Client.Exceptions.Documents.Patching.JavaScriptException: At 4:3 {"name":"RangeError","callstack":" at toString() @  21:3\r\n"} ---> 
        //
        // Note: Use StoreAllFields(FieldStorage.Yes); in index to fix 
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToBeginWithCorrektConditions_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFullNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFullNameMapStartToBegin.Result, BookingIndexFullNameIsFullNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex Me")
                        .Select(x => new {                            
                            Start = x.Begin.ToString(),
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Contains("2018", result.Start);
                }
            });
        }


        // FAIL!
        // Message: System.NullReferenceException : Object reference not set to an instance of an object.  
        [Fact]        
        public void SearchBooking_BookingIndexFullNameIsFullNameMapStartToBeginWithCorrektConditionsToDateTime_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFullNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFullNameMapStartToBegin.Result, BookingIndexFullNameIsFullNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex Me")
                        .Select(x => new {
                            Start = x.Begin,
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal(2018, result.Start.Year);
                }
            });
        }


        // FAIL!
        // Message: Raven.Client.Exceptions.Documents.Patching.JavaScriptException : 
        // Raven.Client.Exceptions.Documents.Patching.JavaScriptException: At 4:3 
        // {"name":"RangeError","callstack":" at toString() @  44:3\r\n"} ---> Jint.Runtime.JavaScriptException
        //
        // Note: Use StoreAllFields(FieldStorage.Yes); in index to fix 
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFirstNameMapStartToBegin_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFirstNameMapStartToBegin());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFirstNameMapStartToBegin.Result, BookingIndexFullNameIsFirstNameMapStartToBegin>()
                        .Where(x => x.FullName == "Alex")
                        .Select(x => new {
                            FullName = x.FullName,
                            Start = x.Begin.ToString(),
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    Assert.Contains("2018", result.Start);
                    Assert.Equal("Alex", result.FullName);
                }
            });
        }

        // WORKS!
        [Fact]
        public void SearchBooking_BookingIndexFullNameIsFirstNameMapStartToStart_ReturnsResult() {
            SetupStore(store => {
                // Arrange  
                store.ExecuteIndex(new BookingIndexFullNameIsFirstNameMapStartToStart());

                using (var session = store.OpenSession()) {
                    session.Advanced.WaitForIndexesAfterSaveChanges(TimeSpan.FromSeconds(2));

                    session.Store(new Booking { FirstName = "Alex", LastName = "Me", Start = DateTime.Parse("2018-01-01T11:11:11") });
                    session.Store(new Booking { FirstName = "You", LastName = "Me", Start = DateTime.Parse("2017-11-11T10:10:10") });
                    session.SaveChanges();
                }

                // Act
                using (var session = store.OpenSession()) {
                    var result = session.Query<BookingIndexFullNameIsFirstNameMapStartToStart.Result, BookingIndexFullNameIsFirstNameMapStartToStart>()
                        .Where(x => x.FullName == "Alex")
                        .Select(x => new {
                            FullName = x.FullName,
                            Start = x.Start.ToString(),
                        }).Single();

                    // Assert
                    Assert.NotNull(result);
                    // Assert.Equal("Alex", result.FullName); // <- This will fail if fields are not stored in index
                }
            });
        }
    }   
}
