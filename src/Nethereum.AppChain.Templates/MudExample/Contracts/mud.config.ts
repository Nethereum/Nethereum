import { defineWorld } from "@latticexyz/world";

export default defineWorld({
  namespace: "app",
  tables: {
    // Singleton table (no key) - global app configuration
    AppConfig: {
      schema: {
        appName: "string",
        version: "string",
        admin: "address",
        paused: "bool",
        maxUsers: "uint256",
      },
      key: [],
    },

    // Single key table - user profiles indexed by address
    UserProfile: {
      schema: {
        user: "address",
        username: "string",
        bio: "string",
        createdAt: "uint256",
        updatedAt: "uint256",
        isActive: "bool",
      },
      key: ["user"],
    },

    // Composite key table - user inventory items
    UserItem: {
      schema: {
        user: "address",
        itemId: "uint256",
        quantity: "uint256",
        acquiredAt: "uint256",
      },
      key: ["user", "itemId"],
    },

    // Array field table - catalog entries with tags
    CatalogEntry: {
      schema: {
        entryId: "uint256",
        name: "string",
        description: "string",
        owner: "address",
        tags: "string[]",
        metadata: "bytes",
      },
      key: ["entryId"],
    },

    // Triple key table - permissions system
    Permission: {
      schema: {
        resource: "bytes32",
        role: "bytes32",
        account: "address",
        granted: "bool",
        grantedBy: "address",
        grantedAt: "uint256",
      },
      key: ["resource", "role", "account"],
    },

    // Counter table - for generating IDs
    Counter: {
      schema: {
        key: "bytes32",
        value: "uint256",
      },
      key: ["key"],
    },
  },
});
