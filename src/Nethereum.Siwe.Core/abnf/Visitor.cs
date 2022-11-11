/* -----------------------------------------------------------------------------
 * Visitor.cs
 * -----------------------------------------------------------------------------
 *
 * Producer : com.parse2.aparse.Parser 2.5
 * Produced : Sat Dec 18 07:35:23 GMT 2021
 *
 * -----------------------------------------------------------------------------
 */

using System;

internal interface Visitor
{
  Object Visit(Rule_sign_in_with_ethereum rule);
  Object Visit(Rule_domain rule);
  Object Visit(Rule_address rule);
  Object Visit(Rule_statement rule);
  Object Visit(Rule_version rule);
  Object Visit(Rule_nonce rule);
  Object Visit(Rule_issued_at rule);
  Object Visit(Rule_expiration_time rule);
  Object Visit(Rule_not_before rule);
  Object Visit(Rule_request_id rule);
  Object Visit(Rule_chain_id rule);
  Object Visit(Rule_resources rule);
  Object Visit(Rule_resource rule);
  Object Visit(Rule_URI rule);
  Object Visit(Rule_hier_part rule);
  Object Visit(Rule_scheme rule);
  Object Visit(Rule_authority rule);
  Object Visit(Rule_userinfo rule);
  Object Visit(Rule_host rule);
  Object Visit(Rule_port rule);
  Object Visit(Rule_IP_literal rule);
  Object Visit(Rule_IPvFuture rule);
  Object Visit(Rule_IPv6address rule);
  Object Visit(Rule_h16 rule);
  Object Visit(Rule_ls32 rule);
  Object Visit(Rule_IPv4address rule);
  Object Visit(Rule_dec_octet rule);
  Object Visit(Rule_reg_name rule);
  Object Visit(Rule_path_abempty rule);
  Object Visit(Rule_path_absolute rule);
  Object Visit(Rule_path_rootless rule);
  Object Visit(Rule_path_empty rule);
  Object Visit(Rule_segment rule);
  Object Visit(Rule_segment_nz rule);
  Object Visit(Rule_pchar rule);
  Object Visit(Rule_query rule);
  Object Visit(Rule_fragment rule);
  Object Visit(Rule_pct_encoded rule);
  Object Visit(Rule_unreserved rule);
  Object Visit(Rule_reserved rule);
  Object Visit(Rule_gen_delims rule);
  Object Visit(Rule_sub_delims rule);
  Object Visit(Rule_date_fullyear rule);
  Object Visit(Rule_date_month rule);
  Object Visit(Rule_date_mday rule);
  Object Visit(Rule_time_hour rule);
  Object Visit(Rule_time_minute rule);
  Object Visit(Rule_time_second rule);
  Object Visit(Rule_time_secfrac rule);
  Object Visit(Rule_time_numoffset rule);
  Object Visit(Rule_time_offset rule);
  Object Visit(Rule_partial_time rule);
  Object Visit(Rule_full_date rule);
  Object Visit(Rule_full_time rule);
  Object Visit(Rule_date_time rule);
  Object Visit(Rule_ALPHA rule);
  Object Visit(Rule_LF rule);
  Object Visit(Rule_DIGIT rule);
  Object Visit(Rule_HEXDIG rule);

  Object Visit(Terminal_StringValue value);
  Object Visit(Terminal_StringExactValue value);
    Object Visit(Terminal_NumericValue value);
}

/* -----------------------------------------------------------------------------
 * eof
 * -----------------------------------------------------------------------------
 */
