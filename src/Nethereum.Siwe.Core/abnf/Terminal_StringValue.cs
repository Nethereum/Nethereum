/* -----------------------------------------------------------------------------
 * Terminal_StringValue.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

internal class Terminal_StringValue:Rule
{
  private Terminal_StringValue(String spelling, List<Rule> rules) :
  base(spelling, rules)
  {
  }

  public static Terminal_StringValue Parse(
    ParserContext context, 
    String regex)
  {
    context.Push("StringValue", regex);

    bool parsed = true;

    Terminal_StringValue stringValue = null;
    try
    {
      String value = context.text.Substring(context.index, regex.Length);

      if ((parsed = value.ToLower().Equals(regex.ToLower())))
      {
        context.index += regex.Length;
        stringValue = new Terminal_StringValue(value, null);
      }
    }
    catch (ArgumentOutOfRangeException) {parsed = false;}

    context.Pop("StringValue", parsed);

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
