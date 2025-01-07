import { defineWorld } from "@latticexyz/world";

export default defineWorld({
  namespace: "MyWorld",
  tables: {
    Counter: {
      schema: {
        value: "uint32",
      },
      key: [],
    },
    Item:{
      schema:{
        id:"uint32",
        price:"uint32",
        name:"string",
        description:"string",
        owner:"string",
      },
      key:["id"]
    }
  },
});
