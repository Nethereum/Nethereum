/* -----------------------------------------------------------------------------
 * Terminal_StringExactValue.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:03:13 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

internal class Terminal_StringExactValue:Rule
{
  private Terminal_StringExactValue(String spelling, List<Rule> rules) :
  base(spelling, rules)
  {
  }

  public static Terminal_StringExactValue Parse(
    ParserContext context, 
    String regex)
  {
    context.Push("StringExactValue", regex);

    bool parsed = true;

    Terminal_StringExactValue stringValue = null;
    try
    {
      String value = context.text.Substring(context.index, regex.Length);

      if ((parsed = value.Equals(regex)))
      {
        context.index += regex.Length;
        stringValue = new Terminal_StringExactValue(value, null);
      }
    }
    catch (ArgumentOutOfRangeException) {parsed = false;}

    context.Pop("StringExactValue", parsed);

    return stringValue;
  }

  internal override Object Accept(Visitor visitor)
  {
    return visitor.Visit(this);
  }
}
/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
