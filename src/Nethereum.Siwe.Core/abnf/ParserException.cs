/* -----------------------------------------------------------------------------
 * ParserException.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

internal class ParserException:Exception
{
  private String reason;
  private String text60;
  private int index60;
  private Stack<String> ruleStack;

  private ParserException cause = null;

  static private readonly String newline = System.Environment.NewLine;

  public ParserException(
    String reason,
    String text,
    int index,
    Stack<String> ruleStack) : base(reason)
  {
    this.reason = reason;
    this.ruleStack = ruleStack;

    int start = (index < 30) ? 0 : index - 30;
    int end = (text.Length < index + 30) ? text.Length : index + 30;
    text60 = text.Substring(start, end - start);
    index60 = (index < 30) ? index : 30;

    Regex regex = new Regex("[\\x00-\\x1F]");
    text60 = regex.Replace(text60, " ");
  }

  public String GetReason()
  {
    return reason;
  }

  public String GetSubstring()
  {
    return text60;
  }

  public int GetSubstringIndex()
  {
    return index60;
  }

  public Stack<String> GetRuleStack()
  {
    return ruleStack;
  }

  public override String Message
  {
    get
    {
      String marker = "                              ";

      StringBuilder buffer = new StringBuilder();
      buffer.Append(reason + newline);
      buffer.Append(text60 + newline);
      buffer.Append(marker.Substring(0, index60) + "^" + newline);

      if (ruleStack.Count > 0)
      {
        buffer.Append("rule stack:");

        foreach (String rule in ruleStack)
          buffer.Append(newline + "  " + rule);
      }

      ParserException secondaryError = (ParserException)GetCause();
      if (secondaryError != null)
      {
        buffer.Append("possible cause: " + secondaryError.reason + newline);
        buffer.Append(secondaryError.text60 + newline);
        buffer.Append(marker.Substring(0, secondaryError.index60) + "^" + newline);

        if (secondaryError.ruleStack.Count > 0)
        {
          buffer.Append("rule stack:");

          foreach (String rule in secondaryError.ruleStack)
            buffer.Append(newline + "  " + rule);
        }
      }

      return buffer.ToString();
    }
  }

  public void SetCause(ParserException cause)
  {
    this.cause = cause;
  }

  ParserException GetCause()
  {
    return cause;
  }
}

/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
