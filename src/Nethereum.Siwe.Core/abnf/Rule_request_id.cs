/* -----------------------------------------------------------------------------
 * Rule_request_id.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

sealed internal class Rule_request_id:Rule
{
  private Rule_request_id(String spelling, List<Rule> rules) :
  base(spelling, rules)
  {
  }

  internal override Object Accept(Visitor visitor)
  {
    return visitor.Visit(this);
  }

  public static Rule_request_id Parse(ParserContext context)
  {
    context.Push("request-id");

    Rule rule;
    bool parsed = true;
    ParserAlternative b;
    int s0 = context.index;
    ParserAlternative a0 = new ParserAlternative(s0);

    List<ParserAlternative> as1 = new List<ParserAlternative>();
    parsed = false;
    {
      int s1 = context.index;
      ParserAlternative a1 = new ParserAlternative(s1);
      parsed = true;
      if (parsed)
      {
        bool f1 = true;
        int c1 = 0;
        while (f1)
        {
          rule = Rule_pchar.Parse(context);
          if ((f1 = rule != null))
          {
            a1.Add(rule, context.index);
            c1++;
          }
        }
        parsed = true;
      }
      if (parsed)
      {
        as1.Add(a1);
      }
      context.index = s1;
    }

    b = ParserAlternative.GetBest(as1);

    parsed = b != null;

    if (parsed)
    {
      a0.Add(b.rules, b.end);
      context.index = b.end;
    }

    rule = null;
    if (parsed)
    {
        rule = new Rule_request_id(context.text.Substring(a0.start, a0.end - a0.start), a0.rules);
    }
    else
    {
        context.index = s0;
    }

    context.Pop("request-id", parsed);

    return (Rule_request_id)rule;
  }
}

/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
