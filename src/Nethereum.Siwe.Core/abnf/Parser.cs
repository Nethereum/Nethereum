/* -----------------------------------------------------------------------------
 * Parser.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

internal class Parser
{
  private Parser() {}


  static public Rule Parse(String rulename, String text)
  {
    return Parse(rulename, text, false);
  }

  static public Rule Parse(String rulename, StreamReader input)
  {
    return Parse(rulename, input, false);
  }



  static private Rule Parse(String rulename, String text, bool trace)
  {
    if (rulename == null)
      throw new ArgumentNullException("null rulename");
    if (text == null)
      throw new ArgumentException("null string");

    ParserContext context = new ParserContext(text, trace);

    Rule rule = null;
    if (rulename.ToLower().Equals("sign-in-with-ethereum".ToLower())) rule = Rule_sign_in_with_ethereum.Parse(context);
    else if (rulename.ToLower().Equals("domain".ToLower())) rule = Rule_domain.Parse(context);
    else if (rulename.ToLower().Equals("address".ToLower())) rule = Rule_address.Parse(context);
    else if (rulename.ToLower().Equals("statement".ToLower())) rule = Rule_statement.Parse(context);
    else if (rulename.ToLower().Equals("version".ToLower())) rule = Rule_version.Parse(context);
    else if (rulename.ToLower().Equals("nonce".ToLower())) rule = Rule_nonce.Parse(context);
    else if (rulename.ToLower().Equals("issued-at".ToLower())) rule = Rule_issued_at.Parse(context);
    else if (rulename.ToLower().Equals("expiration-time".ToLower())) rule = Rule_expiration_time.Parse(context);
    else if (rulename.ToLower().Equals("not-before".ToLower())) rule = Rule_not_before.Parse(context);
    else if (rulename.ToLower().Equals("request-id".ToLower())) rule = Rule_request_id.Parse(context);
    else if (rulename.ToLower().Equals("chain-id".ToLower())) rule = Rule_chain_id.Parse(context);
    else if (rulename.ToLower().Equals("resources".ToLower())) rule = Rule_resources.Parse(context);
    else if (rulename.ToLower().Equals("resource".ToLower())) rule = Rule_resource.Parse(context);
    else if (rulename.ToLower().Equals("URI".ToLower())) rule = Rule_URI.Parse(context);
    else if (rulename.ToLower().Equals("hier-part".ToLower())) rule = Rule_hier_part.Parse(context);
    else if (rulename.ToLower().Equals("scheme".ToLower())) rule = Rule_scheme.Parse(context);
    else if (rulename.ToLower().Equals("authority".ToLower())) rule = Rule_authority.Parse(context);
    else if (rulename.ToLower().Equals("userinfo".ToLower())) rule = Rule_userinfo.Parse(context);
    else if (rulename.ToLower().Equals("host".ToLower())) rule = Rule_host.Parse(context);
    else if (rulename.ToLower().Equals("port".ToLower())) rule = Rule_port.Parse(context);
    else if (rulename.ToLower().Equals("IP-literal".ToLower())) rule = Rule_IP_literal.Parse(context);
    else if (rulename.ToLower().Equals("IPvFuture".ToLower())) rule = Rule_IPvFuture.Parse(context);
    else if (rulename.ToLower().Equals("IPv6address".ToLower())) rule = Rule_IPv6address.Parse(context);
    else if (rulename.ToLower().Equals("h16".ToLower())) rule = Rule_h16.Parse(context);
    else if (rulename.ToLower().Equals("ls32".ToLower())) rule = Rule_ls32.Parse(context);
    else if (rulename.ToLower().Equals("IPv4address".ToLower())) rule = Rule_IPv4address.Parse(context);
    else if (rulename.ToLower().Equals("dec-octet".ToLower())) rule = Rule_dec_octet.Parse(context);
    else if (rulename.ToLower().Equals("reg-name".ToLower())) rule = Rule_reg_name.Parse(context);
    else if (rulename.ToLower().Equals("path-abempty".ToLower())) rule = Rule_path_abempty.Parse(context);
    else if (rulename.ToLower().Equals("path-absolute".ToLower())) rule = Rule_path_absolute.Parse(context);
    else if (rulename.ToLower().Equals("path-rootless".ToLower())) rule = Rule_path_rootless.Parse(context);
    else if (rulename.ToLower().Equals("path-empty".ToLower())) rule = Rule_path_empty.Parse(context);
    else if (rulename.ToLower().Equals("segment".ToLower())) rule = Rule_segment.Parse(context);
    else if (rulename.ToLower().Equals("segment-nz".ToLower())) rule = Rule_segment_nz.Parse(context);
    else if (rulename.ToLower().Equals("pchar".ToLower())) rule = Rule_pchar.Parse(context);
    else if (rulename.ToLower().Equals("query".ToLower())) rule = Rule_query.Parse(context);
    else if (rulename.ToLower().Equals("fragment".ToLower())) rule = Rule_fragment.Parse(context);
    else if (rulename.ToLower().Equals("pct-encoded".ToLower())) rule = Rule_pct_encoded.Parse(context);
    else if (rulename.ToLower().Equals("unreserved".ToLower())) rule = Rule_unreserved.Parse(context);
    else if (rulename.ToLower().Equals("reserved".ToLower())) rule = Rule_reserved.Parse(context);
    else if (rulename.ToLower().Equals("gen-delims".ToLower())) rule = Rule_gen_delims.Parse(context);
    else if (rulename.ToLower().Equals("sub-delims".ToLower())) rule = Rule_sub_delims.Parse(context);
    else if (rulename.ToLower().Equals("date-fullyear".ToLower())) rule = Rule_date_fullyear.Parse(context);
    else if (rulename.ToLower().Equals("date-month".ToLower())) rule = Rule_date_month.Parse(context);
    else if (rulename.ToLower().Equals("date-mday".ToLower())) rule = Rule_date_mday.Parse(context);
    else if (rulename.ToLower().Equals("time-hour".ToLower())) rule = Rule_time_hour.Parse(context);
    else if (rulename.ToLower().Equals("time-minute".ToLower())) rule = Rule_time_minute.Parse(context);
    else if (rulename.ToLower().Equals("time-second".ToLower())) rule = Rule_time_second.Parse(context);
    else if (rulename.ToLower().Equals("time-secfrac".ToLower())) rule = Rule_time_secfrac.Parse(context);
    else if (rulename.ToLower().Equals("time-numoffset".ToLower())) rule = Rule_time_numoffset.Parse(context);
    else if (rulename.ToLower().Equals("time-offset".ToLower())) rule = Rule_time_offset.Parse(context);
    else if (rulename.ToLower().Equals("partial-time".ToLower())) rule = Rule_partial_time.Parse(context);
    else if (rulename.ToLower().Equals("full-date".ToLower())) rule = Rule_full_date.Parse(context);
    else if (rulename.ToLower().Equals("full-time".ToLower())) rule = Rule_full_time.Parse(context);
    else if (rulename.ToLower().Equals("date-time".ToLower())) rule = Rule_date_time.Parse(context);
    else if (rulename.ToLower().Equals("ALPHA".ToLower())) rule = Rule_ALPHA.Parse(context);
    else if (rulename.ToLower().Equals("LF".ToLower())) rule = Rule_LF.Parse(context);
    else if (rulename.ToLower().Equals("DIGIT".ToLower())) rule = Rule_DIGIT.Parse(context);
    else if (rulename.ToLower().Equals("HEXDIG".ToLower())) rule = Rule_HEXDIG.Parse(context);
    else throw new ArgumentException("unknown rule");

    if (rule == null)
    {
      throw new ParserException(
        "rule \"" + (String)context.GetErrorStack().Peek() + "\" failed",
        context.text,
        context.GetErrorIndex(),
        context.GetErrorStack());
    }

    if (context.text.Length > context.index)
    {
      ParserException primaryError = 
        new ParserException(
          "extra data found",
          context.text,
          context.index,
          new Stack<String>());

      if (context.GetErrorIndex() > context.index)
      {
        ParserException secondaryError = 
          new ParserException(
            "rule \"" + (String)context.GetErrorStack().Peek() + "\" failed",
            context.text,
            context.GetErrorIndex(),
            context.GetErrorStack());

        primaryError.SetCause(secondaryError);
      }

      throw primaryError;
    }

    return rule;
  }

  static private Rule Parse(String rulename, StreamReader input, bool trace)
  {
    if (rulename == null)
      throw new ArgumentNullException("null rulename");
    if (input == null)
      throw new ArgumentNullException("null input stream");

    int ch = 0;
    StringBuilder output = new StringBuilder();
    while ((ch = input.Read()) != -1)
      output.Append((char)ch);

    return Parse(rulename, output.ToString(), trace);
  }

}

/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
