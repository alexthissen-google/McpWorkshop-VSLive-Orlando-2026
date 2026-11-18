// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
//
// Workshop note: Duende's quickstart ViewModel also carries a list of external
// identity providers. This workshop has none -- the only identities are the in-memory
// test users (alice and bob) -- so that machinery is removed to keep the page readable.

namespace TravelBooking.IdentityServer.Pages.Login;

public class ViewModel
{
    public bool AllowRememberLogin { get; set; } = true;
    public bool EnableLocalLogin { get; set; } = true;
}
