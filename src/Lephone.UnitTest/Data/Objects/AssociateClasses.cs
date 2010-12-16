﻿using System.Collections.Generic;
using Lephone.Data.Definition;

namespace Lephone.UnitTest.Data.Objects
{
    // HasOne

    [DbTable("People")]
    public class Person : DbObjectModel<Person>
    {
        public string Name { get; set; }

        public HasOne<PersonalComputer> PC;

        public Person()
        {
            PC = new HasOne<PersonalComputer>(this, null, "Person_Id");
        }
    }

    [DbTable("PCs")]
    public class PersonalComputer : DbObjectModel<PersonalComputer>
    {
        public string Name { get; set; }

        [DbColumn("Person_Id")]
        public BelongsTo<Person, long> Owner;

        public PersonalComputer()
        {
            Owner = new BelongsTo<Person, long>(this, "Person_Id");
        }
    }

    [DbTable("People"), DbContext("SqlServerMock")]
    public class PersonSql : DbObjectModel<PersonSql>
    {
        public string Name { get; set; }

        public HasOne<PersonalComputerSql> PC;

        public PersonSql()
        {
            PC = new HasOne<PersonalComputerSql>(this, null, "Person_Id");
        }
    }

    [DbTable("PCs"), DbContext("SqlServerMock")]
    public class PersonalComputerSql : DbObjectModel<PersonalComputerSql>
    {
        public string Name { get; set; }

        [DbColumn("Person_Id")]
        public BelongsTo<PersonSql, long> Owner;

        public PersonalComputerSql()
        {
            Owner = new BelongsTo<PersonSql, long>(this, "Person_Id");
        }
    }

    // HasMany

    [DbTable("Books")]
    public class Book : DbObjectModel<Book>
    {
        public string Name { get; set; }

        [DbColumn("Category_Id")]
        public BelongsTo<Category, long> CurCategory;

        public Book()
        {
            CurCategory = new BelongsTo<Category, long>(this, "Category_Id");
        }
    }

    [DbTable("Categories")]
    public class Category : DbObjectModel<Category>
    {
        public string Name { get; set; }

        public HasMany<Book> Books;

        public Category()
        {
            Books = new HasMany<Book>(this, "Id", "Category_Id");
        }
    }

    [DbTable("Categories")]
    public class Acategory : DbObjectModel<Acategory>
    {
        public string Name { get; set; }

        [HasMany]
        public IList<Abook> Books { get; set; }
    }

    [DbTable("Books")]
    public class Abook : DbObjectModel<Abook>
    {
        public string Name { get; set; }

        [BelongsTo, DbColumn("Category_Id")]
        public Acategory CurCategory { get; set; }
    }

    public class Article : DbObjectModel<Article>
    {
        public string Name { get; set; }

        [HasAndBelongsToMany(OrderBy = "Id")]
        public IList<Reader> Readers { get; set; }
    }

    public class Reader : DbObjectModel<Reader>
    {
        public string Name { get; set; }

        [HasAndBelongsToMany(OrderBy = "Id")]
        public IList<Article> Articles { get; set; }
    }

    public class Article_Reader : IDbObject
    {
        public long Article_Id;
        public long Reader_Id;
    }
}
