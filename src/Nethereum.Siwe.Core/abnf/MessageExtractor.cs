/* -----------------------------------------------------------------------------
 * Displayer.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using Nethereum.Siwe.Core;

internal class MessageExtractor:Visitor
{
    public SiweMessage SiweMessage { get; private set; }
    public MessageExtractor()
    {
        SiweMessage = new SiweMessage();
        SiweMessage.Resources = new List<string>();
    }
    public Object Visit(Rule_sign_in_with_ethereum rule)
  {
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_domain rule)
  { 
      SiweMessage.Domain = rule.spelling;
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_address rule)
  {
      SiweMessage.Address = rule.spelling;
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_statement rule)
  {
      SiweMessage.Statement = rule.spelling;
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_version rule)
  {
      SiweMessage.Version = rule.spelling;
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_nonce rule)
  {
      SiweMessage.Nonce = rule.spelling;
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_issued_at rule)
  {
       SiweMessage.IssuedAt = rule.spelling;
       return VisitRules(rule.rules);
  }

  public Object Visit(Rule_expiration_time rule)
  {
      SiweMessage.ExpirationTime = rule.spelling;
        return VisitRules(rule.rules);
  }

  public Object Visit(Rule_not_before rule)
  {
      SiweMessage.NotBefore = rule.spelling;
        return VisitRules(rule.rules);
  }

  public Object Visit(Rule_request_id rule)
  {
      SiweMessage.RequestId = rule.spelling;
        return VisitRules(rule.rules);
  }

  public Object Visit(Rule_chain_id rule)
  {
      SiweMessage.ChainId = rule.spelling;
        return VisitRules(rule.rules);
  }

  public Object Visit(Rule_resources rule)
  {
    
      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_resource rule)
  {
      //skip "- "
      SiweMessage.Resources.Add(rule.spelling.Substring(2));
      return VisitRules(rule.rules);
  }

  private int _uriCount = 0;
  public Object Visit(Rule_URI rule)
  {
      //first uri found not to be confused with resources
      if (_uriCount == 0)
      {
          SiweMessage.Uri = rule.spelling;
          _uriCount = 1;
      }

      return VisitRules(rule.rules);
  }

  public Object Visit(Rule_hier_part rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_scheme rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_authority rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_userinfo rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_host rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_port rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_IP_literal rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_IPvFuture rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_IPv6address rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_h16 rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_ls32 rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_IPv4address rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_dec_octet rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_reg_name rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_path_abempty rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_path_absolute rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_path_rootless rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_path_empty rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_segment rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_segment_nz rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_pchar rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_query rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_fragment rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_pct_encoded rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_unreserved rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_reserved rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_gen_delims rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_sub_delims rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_date_fullyear rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_date_month rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_date_mday rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_hour rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_minute rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_second rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_secfrac rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_numoffset rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_time_offset rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_partial_time rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_full_date rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_full_time rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_date_time rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_ALPHA rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_LF rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_DIGIT rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Rule_HEXDIG rule)
  {
    return VisitRules(rule.rules);
  }

  public Object Visit(Terminal_StringValue value)
  {
    //Console.Write(value.spelling);
    return null;
  }

  public Object Visit(Terminal_StringExactValue value)
  {
      //Console.Write(value.spelling);
      return null;
  }

    public Object Visit(Terminal_NumericValue value)
  {
    //Console.Write(value.spelling);
    return null;
  }

  private Object VisitRules(List<Rule> rules)
  {
    foreach (Rule rule in rules)
      rule.Accept(this);
    return null;
  }
}

/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
