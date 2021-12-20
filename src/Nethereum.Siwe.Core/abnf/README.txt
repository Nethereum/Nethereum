Use aparse to generate the c# files
https://www.parse2.com/download.shtml

Aparse does not suppport the rule %s (case sensitive) so a new class has been created Terminal_StringExactValue
This needs to be used for all the %s in Rule_sign_in_with_ethereum.cs

Modifications to enable to use aparse in syntax

# instead of ;

all statements finish with ;

remove all %s and add a comment.


Modifications in code generated:
---
Visitor interface needs to add Terminal_StringValue

Object Visit(Terminal_StringExactValue value);
Displayer add Object Visit(Terminal_StringExactValue value);

---
Parser and ParserContext use the console or are main entry for a console app

ParserContext replace Console.WriteLine with Debug.WriteLine
Parser remove Main and any FileStream stuff
---
using file replace all in abnf directory

replace public interface with internal interface
replace public class with internal class
replace public override Object Accept(Visitor with internal override Object Accept(Visitor

--
Delete XmlDisplayer
--
