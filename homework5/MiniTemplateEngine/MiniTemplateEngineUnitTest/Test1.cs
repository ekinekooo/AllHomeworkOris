using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniTemplateEngine;

namespace MiniTemplateEngineUnitTest
{
    [TestClass]
    public sealed class HtmlTemplateRendererTests
    {

        [DataTestMethod]
        [DataRow("<h1>Привет ${Name}</h1><p>Привет ${Name}</p>", "<h1>Привет Kaan</h1><p>Привет Kaan</p>")]
        [DataRow("<h1>Привет ${Name}</h1><p>Привет ${Email}</p>", "<h1>Привет Kaan</h1><p>Привет test@test.ru</p>")]
        [DataRow("<h1>Привет ${Name}</h1><p> группа: ${Group.Name}</p>", "<h1>Привет Kaan</h1><p> группа: 11-409</p>")]
        [DataRow("<h1>Привет $ ${Name}</h1><p> группа: ${Group.Name}$</p>", "<h1>Привет $ Kaan</h1><p> группа: 11-409$</p>")]
        public void RenderFromString_PutVariableInString_ReturnCorrectString(string templateHtml, string expected)
        {
            var testee = CreateRenderer();
            var model = CreateBaseModel();

            var result = testee.RenderFromString(templateHtml, model);


            Assert.AreEqual(expected, result);
        }

        // ---------- RenderFromString: IF / ELSE blocks ----------
        [DataTestMethod]
        // if true
        [DataRow("$if(IsTrue) <p>User is active</p>$endif", " <p>User is active</p>")]
        // if true with else
        [DataRow("$if(IsTrue) <p>User is active</p>$else <p>Not active</p>$endif", " <p>User is active</p>")]
        // if true with variable
        [DataRow("$if(IsTrue) <p>${Name}</p>$endif", " <p>Kaan</p>")]
        // variable before if
        [DataRow("${Email} $if(IsTrue) <p>${Name}</p>$endif", "test@test.ru  <p>Kaan</p>")]
        // variable after if
        [DataRow("$if(IsTrue) <p>${Name}</p>$endif ${Email}", " <p>Kaan</p> test@test.ru")]
        // false branch
        [DataRow("$if(IsFalse) <p>User is active</p>$endif ", " ")]
        // false with else
        [DataRow("$if(IsFalse) <p>User is active</p>$else <p>Not active</p>$endif", " <p>Not active</p>")]
        // false with else + variable
        [DataRow("$if(IsFalse) <p>User is active</p>$else <p>${Name}</p>$endif", " <p>Kaan</p>")]
        // nesting
        [DataRow("$if(IsTrue)$if(IsTrue)${Name}$endif $endif", "Kaan ")]
        [DataRow("$if(IsTrue)$if(IsTrue)${Name}$else Net $endif $endif", "Kaan ")]
        [DataRow("$if(IsTrue)$if(IsFalse)${Name}$else Net $endif $endif", " Net  ")]
        [DataRow("$if(IsFalse)$if(IsFalse)${Name}$else Net $endif $endif", "")]
        [DataRow("$if(IsFalse)$if(IsFalse)${Name}$else Net $endif $else a $endif", " a ")]
        // nested if with variable
        [DataRow("$if(IsTrue)${Email} $if(IsFalse)${Name}$else Net $endif $else a $endif", "test@test.ru  Net  ")]
        public void RenderFromString_IfInTemplate_ReturnCorrectString(string templateHtml, string expected)
        {
            // Arrange
            var testee = CreateRenderer();
            var model = CreateBaseModel(withBooleans: true);

            // Act
            var result = testee.RenderFromString(templateHtml, model);

            // Assert
            Assert.AreEqual(expected, result);
        }

        // ---------- RenderFromString: FOREACH blocks ----------
        [DataTestMethod]
        // foreach
        [DataRow("$foreach(var item in Items) <li>${item.Name}</li> $endfor", " <li>Item 1</li>  <li>Item 2</li> ")]
        // empty collection
        [DataRow("$foreach(var item in EmptyItems) <li>${item.Name}</li> $endfor", "")]
        // variable before/after/inside foreach
        [DataRow("${Name} $foreach(var item in Items) <li>${item.Name}</li> $endfor", "Kaan  <li>Item 1</li>  <li>Item 2</li> ")]
        [DataRow("$foreach(var item in Items) <li>${item.Name}</li> $endfor ${Name}", " <li>Item 1</li>  <li>Item 2</li>  Kaan")]
        [DataRow("$foreach(var item in Items) <li>${item.Name}</li><p>${Name}</p> $endfor", " <li>Item 1</li><p>Kaan</p>  <li>Item 2</li><p>Kaan</p> ")]
        public void RenderFromString_ForeachInTemplate_ReturnCorrectString(string templateHtml, string expected)
        {
            // Arrange
            var testee = CreateRenderer();
            var model = CreateModelWithItems();

            // Act
            var result = testee.RenderFromString(templateHtml, model);

            // Assert
            Assert.AreEqual(expected, result);
        }

        // ---------- RenderFromString: FOREACH + IF combinations ----------
        [DataTestMethod]
        // if inside foreach
        [DataRow("$foreach(var item in Items) $if(IsTrue)<li>${item.Name}</li>$endif $endfor", " <li>Item 1</li>  <li>Item 2</li> ")]
        // foreach inside if
        [DataRow("$if(IsTrue)$foreach(var item in Items)<li>${item.Name}</li>$endfor $endif", "<li>Item 1</li><li>Item 2</li> ")]
        public void RenderFromString_ForeachAndIfInTemplate_ReturnCorrectString(string templateHtml, string expected)
        {
            // Arrange
            var testee = CreateRenderer();
            var model = CreateModelWithItems();

            // Act
            var result = testee.RenderFromString(templateHtml, model);

            // Assert
            Assert.AreEqual(expected, result);
        }

        // ----------------- Helpers -----------------
        private static HtmlTemplateRenderer CreateRenderer() => new HtmlTemplateRenderer();

        private static object CreateBaseModel(bool withBooleans = false)
        {
            var baseModel = new
            {
                Name = "Kaan",
                Email = "test@test.ru",
                Group = new { Id = 1, Name = "11-409" }
            };

            if (!withBooleans) return baseModel;

            return new
            {
                baseModel.Name,
                baseModel.Email,
                baseModel.Group,
                IsTrue = true,
                IsFalse = false
            };
        }

        private static object CreateModelWithItems()
        {
            return new
            {
                Name = "Kaan",
                Email = "test@test.ru",
                Group = new { Id = 1, Name = "11-409" },
                IsTrue = true,
                IsFalse = false,
                Items = new[]
                {
                    new { Name = "Item 1", Email = "Kaan@gmail.com" },
                    new { Name = "Item 2", Email = "Emmedik@gmal.com" }
                },
                EmptyItems = new List<int>()
            };
        }
    }
}
