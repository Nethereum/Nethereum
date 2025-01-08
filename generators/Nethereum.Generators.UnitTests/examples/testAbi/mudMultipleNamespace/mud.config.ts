import { defineWorld } from "@latticexyz/world";

export default defineWorld({
    namespaces: {
        myworld1: {
            tables: {
                Counter: {
                    schema: {
                        value: "uint32",
                    },
                    key: [],
                },
            },
        },
        myworld2: {
            tables: {
                Item: {
                    schema: {
                        id: "uint32",
                        price: "uint32",
                        name: "string",
                        description: "string",
                        owner: "string",
                    },
                    key: ["id"],
                },
            },
        },
    },
});
